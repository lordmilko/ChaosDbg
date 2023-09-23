using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChaosLib;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataProviderBuilderContext
    {
        public Dictionary<IMetaDataImport, Dictionary<mdTypeDef, IWindowsMetadataType>> TypeCache = new Dictionary<IMetaDataImport, Dictionary<mdTypeDef, IWindowsMetadataType>>();

        //Currently we only support a single *.winmd file. If we support more in the future, we need to have a separate typeRefCache per file
        //private Dictionary<mdTypeRef, IWindowsMetadataType> typeRefCache = new Dictionary<mdTypeRef, IWindowsMetadataType>();
        public List<Tuple<MetaDataImport, mdTypeDef>> TypesToDelete = new List<Tuple<MetaDataImport, mdTypeDef>>();
    }

    /// <summary>
    /// Provides access to metadata regarding the Windows SDK stored in *.winmd files.
    /// </summary>
    class WindowsMetadataProvider : IWindowsMetadataProvider
    {
        private string root;

        private IWindowsMetadataType[] allTypes;
        private Dictionary<CorElementType, IWindowsMetadataType> primitiveTypeCache = new Dictionary<CorElementType, IWindowsMetadataType>();

        private List<WindowsMetadataType> apiTypes = new List<WindowsMetadataType>();

        private Dictionary<string, WindowsMetadataField[]> constants;

        private Dictionary<string, WindowsMetadataField[]> Constants
        {
            get
            {
                if (constants == null)
                    constants = apiTypes.SelectMany(a => a.Fields).GroupBy(a => a.Name.ToLower()).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

                return constants;
            }
        }

        private Dictionary<string, WindowsMetadataMethod[]> functions;

        private Dictionary<string, WindowsMetadataMethod[]> Functions
        {
            get
            {
                if (functions == null)
                    functions = apiTypes.SelectMany(a => a.Methods).GroupBy(a => a.Name.ToLower()).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

                return functions;
            }
        }

        private Dictionary<string, WindowsMetadataType[]> interfaces;

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

        #region IWindowsMetadataProvider

        public bool TryGetConstant(string name, out WindowsMetadataField constant)
        {
            EnsureInitialized();

            if (Constants.TryGetValue(name, out var candidates))
            {
                if (candidates.Length > 1)
                    throw new NotSupportedException("Resolving a constant with multiple possible candidates is not currently supported");

                constant = candidates[0];
                return true;
            }

            constant = null;
            return false;
        }

        public bool TryGetFunction(string name, out WindowsMetadataMethod method)
        {
            EnsureInitialized();

            if (Functions.TryGetValue(name, out var candidates))
            {
                if (candidates.Length > 1)
                    throw new NotSupportedException("Resolving a function with multiple possible candidates is not currently supported");

                method = candidates[0];
                return true;
            }

            method = null;
            return false;
        }

        #endregion

        private void EnsureInitialized()
        {
            if (allTypes == null)
                Initialize();
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

            var builderCtx = new WindowsMetadataProviderBuilderContext();

            foreach (var type in types)
                ProcessTypeDef(type, mdi, builderCtx);

            foreach (var item in builderCtx.TypesToDelete)
                builderCtx.TypeCache[item.Item1.Raw].Remove(item.Item2);

            //Done! Now set common lookup values

            allTypes = builderCtx.TypeCache.SelectMany(kv => kv.Value.Select(v => v.Value)).ToArray();

            var fullTypes = allTypes.OfType<WindowsMetadataType>().ToArray();
            var ifaces = fullTypes.Where(f => (f.Flags & CorTypeAttr.tdInterface) != 0).ToArray();            
            interfaces = ifaces.GroupBy(i => i.Name).ToDictionary(g => g.Key.ToLower(), g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        }

        private IWindowsMetadataType ProcessTypeDef(mdTypeDef typeDef, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            if (typeDef.Rid == 0)
                return null;

            var mdiCache = GetMDICache(mdi, builderCtx);

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
                    mdiCache[typeDef] = WindowsMetadataTypeInternal.EnumType;
                    return WindowsMetadataTypeInternal.EnumType;

                case "System.MulticastDelegate":
                    mdiCache[typeDef] = WindowsMetadataTypeInternal.MulticastDelegateType;
                    return WindowsMetadataTypeInternal.MulticastDelegateType;

                case "System.Attribute":
                    mdiCache[typeDef] = WindowsMetadataTypeInternal.DeleteInheritedType;
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
                props
            );

            if (mdi.TryGetNestedClassProps(typeDef, out var enclosingClass) == HRESULT.S_OK)
            {
                var parentType = ProcessTypeDef(enclosingClass, mdi, builderCtx);

                fullType.ParentType = parentType;
            }

            mdiCache[typeDef] = fullType;
            fullType.BaseType = ProcessType(props.ptkExtends, mdi, builderCtx);

            var isEnum = fullType.BaseType == WindowsMetadataTypeInternal.EnumType;

            if (fullType.BaseType != WindowsMetadataTypeInternal.DeleteInheritedType)
                fullType.CustomAttributes = ReadCustomAttribs(typeDef, mdi);

            //This captures fullType, so we if want to change fullType to something else, we need to change
            //how we write this callback
            fullType.SetFields(new Lazy<WindowsMetadataField[]>(() =>
            {
                var fields = new WindowsMetadataField[fieldDefs.Length];

                for (var i = 0; i < fieldDefs.Length; i++)
                {
                    var field = ProcessField(fullType, fieldDefs[i], mdi, builderCtx);

                    fields[i] = field;
                }

                return fields;
            }));

            if (fieldDefs.Length == 1 && mdi.GetFieldProps(fieldDefs[0]).szField == "Value")
            {
                //It's a simple transparent type around a raw value!
                var transparentType = new WindowsMetadataTransparentType(typeDef, props, fullType.Fields[0])
                {
                    CustomAttributes = fullType.CustomAttributes
                };

                mdiCache[typeDef] = transparentType;

                return transparentType;
            }

            if (fullType.BaseType == WindowsMetadataTypeInternal.MulticastDelegateType)
            {
                var delegateType = new WindowsMetadataDelegateType(typeDef, props)
                {
                    CustomAttributes = fullType.CustomAttributes
                };

                //Replace the existing WindowsMetadataType. Since our base class was System.MulticastDelegate, there shouldn't
                //be any references to the previously cached type anywhere
                mdiCache[typeDef] = delegateType;

                return delegateType;
            }
            else if (isEnum)
            {
                var enumType = new WindowsMetadataEnumType(typeDef, props);
                enumType.SetFields(new Lazy<WindowsMetadataField[]>(() =>
                {
                    var list = new List<WindowsMetadataField>();

                    foreach (var fieldDef in fieldDefs)
                    {
                        var field = ProcessField(enumType, fieldDef, mdi, builderCtx);

                        //Skip value__
                        if ((field.Flags & CorFieldAttr.fdLiteral) == 0 && (field.Flags & CorFieldAttr.fdStatic) == 0)
                            continue;

                        list.Add(field);
                    }

                    return list.ToArray();
                }));

                enumType.CustomAttributes = fullType.CustomAttributes;

                //Replace the existing WindowsMetadataType. Since our base class was System.Enum, there shouldn't
                //be any references to the previously cached type anywhere
                mdiCache[typeDef] = enumType;

                return enumType;
            }
            else
            {
                fullType.SetMethods(new Lazy<WindowsMetadataMethod[]>(() =>
                {
                    var methods = new WindowsMetadataMethod[methodDefs.Length];

                    for (var i = 0; i < methodDefs.Length; i++)
                    {
                        var method = ProcessMethod(methodDefs[i], mdi, builderCtx);

                        methods[i] = method;
                    }

                    return methods;
                }));
            }

            if (fullType.Name == "Apis")
                apiTypes.Add(fullType);

            if (ShouldDeleteType(fullType))
                builderCtx.TypesToDelete.Add(Tuple.Create(mdi, typeDef));

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

        private IWindowsMetadataType ProcessTypeRef(mdTypeRef typeRef, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            if (typeRef.Rid == 0)
                return null;

            var resolvedRef = typeRefResolver.ResolveTypeRef(typeRef, mdi);

            if (typeRefResolver.ToRemove.Contains(typeRef))
                return WindowsMetadataTypeInternal.DeleteInheritedType;

            if (resolvedRef == null)
                return null;

            if (resolvedRef is ResolvedTypeRef r)
                return ProcessTypeDef(r.TypeDef, resolvedRef.TypeDefModule.Import, builderCtx);

            var ambiguous = (AmbiguousResolvedTypeRef) resolvedRef;

            var results = ambiguous.TypeDefs.Select(v => ProcessTypeDef(v, resolvedRef.TypeDefModule.Import, builderCtx)).ToArray();

            return new WindowsMetadataAmbiguousType(results);
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

        private IWindowsMetadataType ProcessType(mdToken token, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            switch (token.Type)
            {
                case CorTokenType.mdtTypeDef:
                    return ProcessTypeDef((mdTypeDef) token, mdi, builderCtx);

                case CorTokenType.mdtTypeRef:
                    return ProcessTypeRef((mdTypeRef) token, mdi, builderCtx);

                default:
                    throw new NotImplementedException($"Don't know how to handle token of type {token.Type}");
            }
        }

        private WindowsMetadataField ProcessField(WindowsMetadataType owner, mdFieldDef fieldDef, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            var props = mdi.GetFieldProps(fieldDef);

            var sig = sigReader.ReadField(fieldDef, mdi, props);

            var type = ConvertSigTypeToWinMDType(sig.Type, mdi, builderCtx);
            var customAttribs = ReadCustomAttribs(fieldDef, mdi);

            if (type is WindowsMetadataAmbiguousType a)
            {
                foreach (var item in a.Candidates)
                {
                    if (item is WindowsMetadataType t && t.ParentType is WindowsMetadataType p && p.TypeDef == props.pClass)
                    {
                        type = item;
                        break;
                    }
                }
            }

            return new WindowsMetadataField(
                owner,
                fieldDef,
                props,
                type,
                customAttribs
            );
        }

        private WindowsMetadataMethod ProcessMethod(mdMethodDef methodDef, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            var props = mdi.GetMethodProps(methodDef);
            var sig = sigReader.ReadMethod(methodDef, mdi);

            var returnType = ConvertSigTypeToWinMDType(sig.RetType, mdi, builderCtx);

            //You can't get the parameter type from MDI, so we have to use the sigblob

            IWindowsMetadataParameter[] parameters;

            if (sig.Parameters.Length == 0)
                parameters = Array.Empty<IWindowsMetadataParameter>();
            else
            {
                parameters = new IWindowsMetadataParameter[sig.Parameters.Length];

                for (var i = 0; i < sig.Parameters.Length; i++)
                    parameters[i] = ProcessParameter(sig.Parameters[i], mdi, builderCtx);
            }

            var customAttribs = ReadCustomAttribs(methodDef, mdi);

            var method = new WindowsMetadataMethod(methodDef, props, returnType, parameters, customAttribs);

            return method;
        }

        private IWindowsMetadataParameter ProcessParameter(ISigParameter parameter, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            if (parameter is ISigArgListParameter)
                return new WindowsMetadataArgListParameter();

            var type = ConvertSigTypeToWinMDType(parameter.Type, mdi, builderCtx);

            if (parameter is ISigNormalParameter sigNormal)
                return new WindowsMetadataNormalParameter(sigNormal.Info, type);
            if (parameter is ISigFnPtrParameter)
                return new WindowsMetadataFnPtrParameter(type);
            else
                throw new NotImplementedException($"Don't know how to handle parameter of type {parameter.GetType().Name}");
        }

        private IWindowsMetadataType ConvertSigTypeToWinMDType(ISigType sigType, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
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
                    return GetArrayMetadataType((ISigArrayType) sigType, mdi, builderCtx);

                #endregion
                #region CLASS TypeDefOrRefOrSpecEncoded | VALUETYPE TypeDefOrRefOrSpecEncoded

                case CorElementType.Class:
                    return ProcessType(((ISigClassType) sigType).Token, mdi, builderCtx);

                case CorElementType.ValueType:
                    return ProcessType(((ISigValueType) sigType).Token, mdi, builderCtx);

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
                    return new WindowsMetadataPointerType(ConvertSigTypeToWinMDType(((ISigPtrType) sigType).PtrType, mdi, builderCtx));

                #endregion
                #region SZARRAY CustomMod* Type (single dimensional, zero-based array i.e., vector)

                case CorElementType.SZArray:
                    return GetSZArrayMetadataType((ISigSZArrayType) sigType, mdi, builderCtx);

                #endregion

                case CorElementType.ByRef:
                    return new WindowsMetadataByRefType(ConvertSigTypeToWinMDType(((ISigRefType) sigType).InnerType, mdi, builderCtx));

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

        private IWindowsMetadataType GetArrayMetadataType(ISigArrayType sigType, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            var elementType = ConvertSigTypeToWinMDType(sigType.ElementType, mdi, builderCtx);

            return new WindowsMetadataArrayType(elementType, sigType.Rank, sigType.Sizes, sigType.LowerBounds);
        }

        private IWindowsMetadataType GetSZArrayMetadataType(ISigSZArrayType sigType, MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            var elementType = ConvertSigTypeToWinMDType(sigType.ElementType, mdi, builderCtx);

            return new WindowsMetadataSZArrayType(elementType);
        }

        private Dictionary<mdTypeDef, IWindowsMetadataType> GetMDICache(MetaDataImport mdi, WindowsMetadataProviderBuilderContext builderCtx)
        {
            if (!builderCtx.TypeCache.TryGetValue(mdi.Raw, out var value))
            {
                value = new Dictionary<mdTypeDef, IWindowsMetadataType>();
                builderCtx.TypeCache[mdi.Raw] = value;
            }

            return value;
        }
    }
}
