using System;
using System.Collections.Generic;
using System.IO;
using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    /// <summary>
    /// Provides access to metadata regarding the Windows SDK stored in *.winmd files.
    /// </summary>
    class WindowsMetadataProvider : IWindowsMetadataProvider
    {
        private string root;

        private Dictionary<IMetaDataImport, Dictionary<mdTypeDef, IWindowsMetadataType>> typeCache = new Dictionary<IMetaDataImport, Dictionary<mdTypeDef, IWindowsMetadataType>>();
        private Dictionary<CorElementType, IWindowsMetadataType> primitiveTypeCache = new Dictionary<CorElementType, IWindowsMetadataType>();

        //Currently we only support a single *.winmd file. If we support more in the future, we need to have a separate typeRefCache per file
        //private Dictionary<mdTypeRef, IWindowsMetadataType> typeRefCache = new Dictionary<mdTypeRef, IWindowsMetadataType>();
        private List<Tuple<MetaDataImport, mdTypeDef>> typesToDelete = new List<Tuple<MetaDataImport, mdTypeDef>>();

        private List<WindowsMetadataType> apiTypes = new List<WindowsMetadataType>();

        private ISigReader sigReader;

        private WinMDTypeRefResolver typeRefResolver = new WinMDTypeRefResolver();

        public WindowsMetadataProvider(ISigReader sigReader)
        {
            if (sigReader == null)
                throw new ArgumentNullException(nameof(sigReader));

            root = Path.Combine(
                Path.GetDirectoryName(typeof(NativeLibraryProvider).Assembly.Location),
                "winmd"
            );

            this.sigReader = sigReader;
        }

        private void Initialize()
        {
            //Currently we only support a single *.winmd file. If we support more in the future, we need to have a separate typeRefCache per file
            var win32 = Path.Combine(root, "Windows.Win32.winmd");

            if (!File.Exists(win32))
                throw new FileNotFoundException($"Failed to find winmd file '{win32}'", win32);

            //Assembly.LoadFrom will take a bit of time, and there's evidently multiple types
            //that have the same name. As such, we'll parse the file manually ourselves. This has
            //the benefit of reducing memory usage (we only have to store our object model) and
            //also lets us unload this data if we want

            var disp = new MetaDataDispenserEx();

            var mdi = disp.OpenScope<MetaDataImport>(win32, CorOpenFlags.ofReadOnly);

            var types = mdi.EnumTypeDefs();

            foreach (var type in types)
            {
                ProcessTypeDef(type, mdi);
            }

            foreach (var item in typesToDelete)
                typeCache[item.Item1.Raw].Remove(item.Item2);

            typesToDelete = null;
        }

        private IWindowsMetadataType ProcessTypeDef(mdTypeDef typeDef, MetaDataImport mdi)
        {
            if (typeDef.Rid == 0)
                return null;

            var mdiCache = GetMDICache(mdi);

            if (mdiCache.TryGetValue(typeDef, out var type))
                return type;

            var props = mdi.GetTypeDefProps(typeDef);

            switch (props.szTypeDef)
            {
                case "System.Object":
                    type = new WindowsMetadataPrimitiveType(CorElementType.Object);
                    mdiCache[typeDef] = type;
                    return type;

                case "System.Enum":
                case "System.MulticastDelegate":
                    mdiCache[typeDef] = null;
                    return null;

                case "System.Attribute":
                    return WindowsMetadataTypeInternal.DeleteInheritedType;

                case "System.Guid":
                    type = new WindowsMetadataSpecialType(WindowsMetadataSpecialKind.Guid);
                    mdiCache[typeDef] = type;
                    return type;

                case "System.String":
                    type = new WindowsMetadataPrimitiveType(CorElementType.String);
                    mdiCache[typeDef] = type;
                    return type;
            }

            var fieldDefs = mdi.EnumFields(typeDef);
            var methodDefs = mdi.EnumMethods(typeDef);

            var fullType = new WindowsMetadataType(
                typeDef,
                props,
                fieldDefs.Length,
                methodDefs.Length
            );

            mdiCache[typeDef] = fullType;

            fullType.BaseType = ProcessType(props.ptkExtends, mdi);

            if (fullType.BaseType != WindowsMetadataTypeInternal.DeleteInheritedType)
                fullType.CustomAttributes = ReadCustomAttribs(typeDef, mdi);

            for (var i = 0; i < fieldDefs.Length; i++)
            {
                var field = ProcessField(fieldDefs[i], mdi);

                fullType.Fields[i] = field;
            }

            for (var i = 0; i < methodDefs.Length; i++)
            {
                var method = ProcessMethod(methodDefs[i], mdi);

                fullType.Methods[i] = method;
            }

            if (fullType.Name == "Apis")
                apiTypes.Add(fullType);

            if (ShouldDeleteType(fullType))
                typesToDelete.Add(Tuple.Create(mdi, typeDef));

            return fullType;
        }

        private ISigCustomAttribute[] ReadCustomAttribs(mdToken token, MetaDataImport mdi)
        {
            var customAttribs = mdi.EnumCustomAttributes(token, mdToken.Nil);

            if (customAttribs.Length == 0)
                return Array.Empty<ISigCustomAttribute>();

            var list = new List<ISigCustomAttribute>();

            foreach (var attrib in customAttribs)
                list.Add(sigReader.ReadCustomAttribute(attrib, mdi, typeRefResolver));

            return list.ToArray();
        }

        private IWindowsMetadataType ProcessTypeRef(mdTypeRef typeRef, MetaDataImport mdi)
        {
            if (typeRef.Rid == 0)
                return null;

            var resolvedRef = typeRefResolver.ResolveTypeRef(typeRef, mdi);

            if (typeRefResolver.ToRemove.Contains(typeRef))
                return WindowsMetadataTypeInternal.DeleteInheritedType;

            if (resolvedRef == null)
                return null;

            return ProcessTypeDef(resolvedRef.TypeDef, resolvedRef.TypeDefModule.Import);
        }

        private bool ShouldDeleteType(IWindowsMetadataType type)
        {
            var parent = type;

            while (true)
            {
                if (parent == null)
                    break;

                if (ReferenceEquals(parent, WindowsMetadataTypeInternal.DeleteInheritedType))
                    return true;

                if (parent is WindowsMetadataType t)
                    parent = t.BaseType;
                else
                    break; //Can't go any further back
            }

            return false;
        }

        private IWindowsMetadataType ProcessType(mdToken token, MetaDataImport mdi)
        {
            switch (token.Type)
            {
                case CorTokenType.mdtTypeDef:
                    return ProcessTypeDef((mdTypeDef) token, mdi);

                case CorTokenType.mdtTypeRef:
                    return ProcessTypeRef((mdTypeRef) token, mdi);

                default:
                    throw new NotImplementedException($"Don't know how to handle token of type {token.Type}");
            }
        }

        private WindowsMetadataField ProcessField(mdFieldDef fieldDef, MetaDataImport mdi)
        {
            var props = mdi.GetFieldProps(fieldDef);

            var type = ProcessTypeDef(props.pClass, mdi);
            var customAttribs = ReadCustomAttribs(fieldDef, mdi);

            return new WindowsMetadataField(
                fieldDef,
                props,
                type,
                customAttribs
            );
        }

        private WindowsMetadataMethod ProcessMethod(mdMethodDef methodDef, MetaDataImport mdi)
        {
            var props = mdi.GetMethodProps(methodDef);
            var sig = sigReader.ReadMethod(methodDef, mdi);

            var returnType = ConvertSigTypeToWinMDType(sig.RetType, mdi);

            //You can't get the parameter type from MDI, so we have to use the sigblob

            IWindowsMetadataParameter[] parameters;

            if (sig.Parameters.Length == 0)
                parameters = Array.Empty<IWindowsMetadataParameter>();
            else
            {
                parameters = new IWindowsMetadataParameter[sig.Parameters.Length];

                for (var i = 0; i < sig.Parameters.Length; i++)
                    parameters[i] = ProcessParameter(sig.Parameters[i], mdi);
            }

            var customAttribs = ReadCustomAttribs(methodDef, mdi);

            var method = new WindowsMetadataMethod(methodDef, props, returnType, parameters, customAttribs);

            return method;
        }

        private IWindowsMetadataParameter ProcessParameter(ISigParameter parameter, MetaDataImport mdi)
        {
            if (parameter is ISigArgListParameter)
                return new WindowsMetadataArgListParameter();

            var type = ConvertSigTypeToWinMDType(parameter.Type, mdi);

            if (parameter is ISigNormalParameter sigNormal)
                return new WindowsMetadataNormalParameter(sigNormal.Info, type);
            if (parameter is ISigFnPtrParameter)
                return new WindowsMetadataFnPtrParameter(type);
            else
                throw new NotImplementedException($"Don't know how to handle parameter of type {parameter.GetType().Name}");
        }

        private IWindowsMetadataType ConvertSigTypeToWinMDType(ISigType sigType, MetaDataImport mdi)
        {
            switch (sigType.Type)
            {
                #region BOOLEAN | CHAR | I1 | U1 | I2 | U2 | I4 | U4 | I8 | U8 | R4 | R8 | I | U

                case CorElementType.Boolean:
                case CorElementType.Char:
                case CorElementType.I1:
                case CorElementType.U1:
                case CorElementType.I2:
                case CorElementType.U2:
                case CorElementType.I4:
                case CorElementType.U4:
                case CorElementType.I8:
                case CorElementType.U8:
                case CorElementType.R4:
                case CorElementType.R8:
                case CorElementType.I:
                case CorElementType.U:
                    return GetPrimitiveMetadataType(sigType.Type);

                #endregion
                #region ARRAY Type ArrayShape (general array, see §II.23.2.13)

                case CorElementType.Array:
                    //SigArrayType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                #endregion
                #region CLASS TypeDefOrRefOrSpecEncoded | VALUETYPE TypeDefOrRefOrSpecEncoded

                case CorElementType.Class:
                    return ProcessType(((ISigClassType) sigType).Token, mdi);

                case CorElementType.ValueType:
                    return ProcessType(((ISigValueType) sigType).Token, mdi);

                #endregion
                #region FNPTR MethodDefSig | FNPTR MethodRefSig

                case CorElementType.FnPtr:
                    //SigFnPtrType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                #endregion
                #region GENERICINST (CLASS | VALUETYPE) TypeDefOrRefOrSpecEncoded GenArgCount Type*

                case CorElementType.GenericInst:
                    //SigGenericType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                #endregion
                #region MVAR number | VAR number

                case CorElementType.MVar:
                    //SigMethodGenericArgType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                case CorElementType.Var:
                    //SigTypeGenericArgType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                #endregion
                #region OBJECT | STRING

                case CorElementType.Object:
                case CorElementType.String:
                    return GetPrimitiveMetadataType(sigType.Type);

                #endregion
                #region PTR CustomMod* Type | PTR CustomMod* VOID

                case CorElementType.Ptr:
                    return new WindowsMetadataPointerType(ConvertSigTypeToWinMDType(((ISigPtrType) sigType).PtrType, mdi));

                #endregion
                #region SZARRAY CustomMod* Type (single dimensional, zero-based array i.e., vector)

                case CorElementType.SZArray:
                    return GetSZArrayMetadataType((ISigSZArrayType) sigType, mdi);

                #endregion

                case CorElementType.ByRef:
                    return new WindowsMetadataByRefType(ConvertSigTypeToWinMDType(((ISigRefType) sigType).InnerType, mdi));

                //A RetType includes either a [ByRef] Type / TypedByRef / Void
                case CorElementType.Void:
                case CorElementType.TypedByRef:
                    return GetPrimitiveMetadataType(sigType.Type);

                //Contrary to the original ECMA 335 spec, this can occur before or after ByRef
                //https://github.com/dotnet/runtime/blob/main/docs/design/specs/Ecma-335-Augments.md#4-byref-can-come-before-custom-modifiers
                case CorElementType.CModReqd:
                case CorElementType.CModOpt:
                    //SigModType
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                case CorElementType.Sentinel:
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");

                default:
                    throw new NotImplementedException($"Don't know how to handle type '{sigType.Type}'");
            }
        }

        private IWindowsMetadataType GetPrimitiveMetadataType(CorElementType type)
        {
            if (primitiveTypeCache.TryGetValue(type, out var value))
                return value;

            value = new WindowsMetadataPrimitiveType(type);
            primitiveTypeCache[type] = value;
            return value;
        }

        private IWindowsMetadataType GetSZArrayMetadataType(ISigSZArrayType sigType, MetaDataImport mdi)
        {
            var elementType = ConvertSigTypeToWinMDType(sigType.ElementType, mdi);

            return new WindowsMetadataSZArrayType(elementType);
        }

        private Dictionary<mdTypeDef, IWindowsMetadataType> GetMDICache(MetaDataImport mdi)
        {
            if (!typeCache.TryGetValue(mdi.Raw, out var value))
            {
                value = new Dictionary<mdTypeDef, IWindowsMetadataType>();
                typeCache[mdi.Raw] = value;
            }

            return value;
        }
    }
}
