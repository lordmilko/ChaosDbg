using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ClrDebug;

namespace ChaosDbg.Metadata
{
    public struct SigReaderInternal
    {
        private IntPtr currentSigBlob;
        internal IntPtr originalSigBlob;

        internal int Length { get; }

        internal int Read => (int) (currentSigBlob.ToInt64() - originalSigBlob.ToInt64());

        internal bool Completed => Read == Length;

        internal MetaDataImport Import { get; }

        internal mdToken Token { get; }

        public SigReaderInternal(IntPtr sigBlob, int sigBlobLength, mdToken token, MetaDataImport import)
        {
            currentSigBlob = sigBlob;
            originalSigBlob = sigBlob;
            Length = sigBlobLength;
            Token = token;
            Import = import;
        }

        public SigMethod ParseMethod(string name, bool topLevel)
        {
            string[] genericTypeArgs = null;

            //The first byte of the Signature holds bits for HASTHIS, EXPLICITTHIS and calling convention (DEFAULT,
            //VARARG, or GENERIC). These are ORed together.
            var callingConvention = (CallingConvention) CorSigUncompressCallingConv();

            if (callingConvention.IsGeneric)
            {
                var genParamCount = CorSigUncompressData();

                if (Token.Type == CorTokenType.mdtModule)
                {
                    var genericParams = Import.EnumGenericParams((mdMethodDef) Token);

                    var list = new List<string>();

                    foreach (var genericParam in genericParams)
                    {
                        list.Add(Import.GetGenericParamProps(genericParam).wzname);
                    }

                    genericTypeArgs = list.ToArray();
                }
            }

            var paramCount = CorSigUncompressData();

            var retType = SigType.New(ref this);

            var methodParams = ParseSigMethodParams(paramCount, topLevel, callingConvention);

            if (callingConvention.IsVarArg && Token.Type == CorTokenType.mdtMethodDef)
                methodParams.normal.Add(new SigArgListParameter());

            if (methodParams.vararg == null || methodParams.vararg.Count == 0)
                return new SigMethodDef(name, callingConvention, retType, methodParams.normal.ToArray(), genericTypeArgs);
            else
                return new SigMethodRef(name, callingConvention, retType, methodParams.normal.ToArray(), methodParams.vararg.ToArray());
        }

        public ISigField ParseField(string name)
        {
            var callingConvention = (CallingConvention)CorSigUncompressCallingConv();

            var type = SigType.New(ref this);

            return new SigField(name, type, callingConvention);
        }

        public ISigCustomAttribute ParseCustomAttribute(mdToken member, ITypeRefResolver typeRefResolver)
        {
            //Unlike other signatures, where numbers are compressed, here we are reading
            //UNCOMPRESSED values

            var prolog = ReadValue<ushort>();

            if (prolog != 1)
                throw new InvalidOperationException($"Expected custom attribute sigblob to start with 1, however started with '{prolog}' instead");

            mdMethodDef methodDef;
            MetaDataImport methodImport = Import;

            switch (member.Type)
            {
                case CorTokenType.mdtMemberRef:
                    var resolvedMember = typeRefResolver.ResolveMemberRef((mdMemberRef) member, Import);

                    if (resolvedMember == null)
                        return SigCustomAttribute.UnsupportedAttribute;

                    methodDef = (mdMethodDef) resolvedMember.Token;
                    methodImport = resolvedMember.TokenModule.Import;
                    break;

                case CorTokenType.mdtMethodDef:
                    methodDef = (mdMethodDef) member;
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to resolve custom attribute ctor from token of type '{member.Type}'.");
            }

            var methodProps = methodImport.GetMethodProps(methodDef);
            var method = new SigReaderInternal(methodProps.ppvSigBlob, methodProps.pcbSigBlob, methodDef, methodImport).ParseMethod(methodProps.szMethod, true);

            var attribType = methodImport.GetTypeDefProps(methodProps.pClass);

            var fixedArgs = new List<ISigCustomAttribFixedArg>();

            foreach (var parameter in method.Parameters)
                fixedArgs.Add(ParseCustomAttribFixedArg((CorSerializationType) parameter.Type.Type, parameter.Type, methodImport, typeRefResolver));

            var namedArgs = new List<ISigCustomAttribNamedArg>();
            var numNamed = ReadValue<ushort>();

            for (var i = 0; i < numNamed; i++)
                namedArgs.Add(ParseCustomAttribNamedArg(typeRefResolver));

            return new SigCustomAttribute(
                attribType.szTypeDef,
                fixedArgs.ToArray(),
                namedArgs.ToArray()
            );
        }

        private ISigCustomAttribFixedArg ParseCustomAttribFixedArg(CorSerializationType type, ISigType sigType, MetaDataImport ctorImport, ITypeRefResolver typeRefResolver)
        {
            if (sigType != null && sigType.Type == CorElementType.SZArray)
            {
                var numElem = CorSigUncompressData();

                var items = new List<ISigCustomAttribFixedArg>();

                var arr = (ISigSZArrayType)sigType;

                for (var i = 0; i < numElem; i++)
                    items.Add(ParseCustomAttribElem((CorSerializationType) arr.ElementType.Type, arr, ctorImport, typeRefResolver));

                return new SigCustomAttribSZArrayFixedArg(items.ToArray());
            }
            else
                return ParseCustomAttribElem(type, sigType, ctorImport, typeRefResolver);
        }

        private ISigCustomAttribFixedArg ParseCustomAttribElem(CorSerializationType type, ISigType sigType, MetaDataImport ctorImport, ITypeRefResolver typeRefResolver)
        {
            //https://www.ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf
            //p294

            object GetValue(ref SigReaderInternal reader)
            {
                switch ((CorSerializationType) type)
                {
                    case CorSerializationType.SERIALIZATION_TYPE_BOOLEAN:
                        return new SigCustomAttribFixedArg(reader.ReadValue<byte>() == 1);

                    case CorSerializationType.SERIALIZATION_TYPE_CHAR:
                        return reader.ReadValue<char>();

                    case CorSerializationType.SERIALIZATION_TYPE_I1:
                        return reader.ReadValue<sbyte>();

                    case CorSerializationType.SERIALIZATION_TYPE_U1:
                        return reader.ReadValue<byte>();

                    case CorSerializationType.SERIALIZATION_TYPE_I2:
                        return reader.ReadValue<short>();

                    case CorSerializationType.SERIALIZATION_TYPE_U2:
                        return reader.ReadValue<ushort>();

                    case CorSerializationType.SERIALIZATION_TYPE_I4:
                        return reader.ReadValue<int>();

                    case CorSerializationType.SERIALIZATION_TYPE_U4:
                        return reader.ReadValue<uint>();

                    case CorSerializationType.SERIALIZATION_TYPE_I8:
                        return reader.ReadValue<long>();

                    case CorSerializationType.SERIALIZATION_TYPE_U8:
                        return reader.ReadValue<ulong>();

                    case CorSerializationType.SERIALIZATION_TYPE_R4:
                        return reader.ReadValue<float>();

                    case CorSerializationType.SERIALIZATION_TYPE_R8:
                        return reader.ReadValue<double>();

                    case CorSerializationType.SERIALIZATION_TYPE_STRING:
                        return reader.ReadSerString();

                    case (CorSerializationType) CorElementType.Class:
                        return reader.ParseCustomAttribClassType((ISigClassType) sigType);

                    case (CorSerializationType) CorElementType.ValueType:
                        return reader.ParseCustomAttribValueTypeElem((ISigValueType) sigType, ctorImport, typeRefResolver);

                    default:
                        throw new NotImplementedException($"Don't know how to handle element of type '{type}'");
                }
            }

            return new SigCustomAttribFixedArg(GetValue(ref this));
        }

        private ISigCustomAttribFixedArg ParseCustomAttribValueTypeElem(ISigValueType valueType, MetaDataImport ctorImport, ITypeRefResolver typeRefResolver)
        {
            MetaDataImport mdi;
            mdTypeDef typeDef;

            switch (valueType.Token.Type)
            {
                case CorTokenType.mdtTypeDef:
                    mdi = ctorImport;
                    typeDef = (mdTypeDef) valueType.Token;
                    break;

                case CorTokenType.mdtTypeRef:
                    var resolvedTypeRef = typeRefResolver.ResolveTypeRef((mdTypeRef) valueType.Token, ctorImport);
                    mdi = resolvedTypeRef.TypeDefModule.Import;
                    typeDef = resolvedTypeRef.TypeDef;
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to resolve enum from token of type {valueType.Token.Type}");
            }

            var typeProps = mdi.GetTypeDefProps(typeDef);

            return ParseCustomAttribEnumElem(typeProps.szTypeDef, typeDef, mdi, typeRefResolver);
        }

        private ISigCustomAttribFixedArg ParseCustomAttribEnumElem(string name, mdTypeDef typeDef, MetaDataImport ctorImport, ITypeRefResolver typeRefResolver)
        {
            var fields = ctorImport.EnumFields(typeDef);

            var mdi = ctorImport;

            var value__ = fields.Select(v => new
            {
                Token = v,
                Props = mdi.GetFieldProps(v)
            }).Single(
                v => (v.Props.pdwAttr & CorFieldAttr.fdLiteral) == 0 && (v.Props.pdwAttr & CorFieldAttr.fdStatic) == 0
            );

            var underlyingType = new SigReaderInternal(
                value__.Props.ppvSigBlob,
                value__.Props.pcbSigBlob,
                value__.Token,
                mdi
            ).ParseField(value__.Props.szField).Type;

            return new SigCustomAttribEnumFixedArg(name, ParseCustomAttribElem((CorSerializationType) underlyingType.Type, underlyingType, ctorImport, typeRefResolver));
        }

        private ISigCustomAttribFixedArg ParseCustomAttribClassType(ISigClassType classType)
        {
            if (classType.Name != "System.Type")
                throw new NotImplementedException($"Don't know how to handle class value of type '{classType.Name}'");

            var name = ReadSerString();

            return new SigCustomAttribTypeFixedArg(name);
        }

        private ISigCustomAttribNamedArg ParseCustomAttribNamedArg(ITypeRefResolver typeRefResolver)
        {
            var memberKind = (CorSerializationType) CorSigUncompressData();
            var valueType = (CorSerializationType) CorSigUncompressData();

            if (valueType == CorSerializationType.SERIALIZATION_TYPE_SZARRAY)
                throw new NotImplementedException("Don't know how to handle having a named arg containing an array. Our current design wants to pass null for the sigtype, which will explode when attempting to handle arrays");

            var name = ReadSerString();

            var value = ParseCustomAttribFixedArg(valueType, null, Import, typeRefResolver);

            return new SigCustomAttribNamedArg(memberKind, name, value);
        }

        private unsafe string ReadSerString()
        {
            if (*(byte*)currentSigBlob == 0xFF)
                throw new NotImplementedException("Null strings have not been tested. Will 0xFF be followed by 3 zero bytes, or is the value a single 0xFF byte?");

            var length = CorSigUncompressData();

            if (length == 0)
                return string.Empty;

            var bytes = new List<byte>();

            for (var i = 0; i < length; i++)
            {
                bytes.Add(*(byte*)currentSigBlob);
                currentSigBlob = currentSigBlob + 1;
            }

            var str = Encoding.UTF8.GetString(bytes.ToArray());

            return str;
        }

        private unsafe T ReadValue<T>() where T : unmanaged
        {
            var value = *(T*)currentSigBlob;
            currentSigBlob += Marshal.SizeOf<T>();
            return value;
        }

        private (List<ISigParameter> normal, List<ISigParameter> vararg) ParseSigMethodParams(int sigParamCount, bool topLevel, CallingConvention callingConvention)
        {
            if (sigParamCount == 0)
                return (new List<ISigParameter>(), null);

            GetParamPropsResult[] metaDataParameters = null;

            if (topLevel && Token.Type == CorTokenType.mdtMethodDef)
            {
                var import = Import;
                metaDataParameters = Import.EnumParams((mdMethodDef) Token).Select(p => import.GetParamProps(p)).ToArray();
            }
            
            var normal = new List<ISigParameter>();
            var varargs = new List<ISigParameter>();

            var list = normal;
            bool haveSentinel = false;

            for (var i = 0; i < sigParamCount; i++)
            {
                var sigType = SigType.New(ref this);

                if (sigType == SigType.Sentinel)
                {
                    list = varargs;
                    haveSentinel = true;
                    sigType = SigType.New(ref this);
                }

                if (haveSentinel)
                {
                    list.Add(new SigVarArgParameter(sigType));
                    continue;
                }

                if (topLevel)
                {
                    if (Token.Type == CorTokenType.mdtMethodDef)
                    {
                        GetParamPropsResult metaDataParam = default(GetParamPropsResult);

                        if (i < metaDataParameters.Length)
                        {
                            metaDataParam = metaDataParameters[i];

                            if (metaDataParam.pulSequence != i + 1)
                                metaDataParam = metaDataParameters.FirstOrDefault(p => p.pulSequence == i + 1);
                        }
                        else
                        {
                            if (metaDataParameters.Length > 0)
                                metaDataParam = metaDataParameters.FirstOrDefault(p => p.pulSequence == i + 1);
                        }

                        list.Add(new SigNormalParameter(sigType, metaDataParam.Equals(default(GetParamPropsResult)) ? null : (GetParamPropsResult?) metaDataParam));
                    }
                    else
                        list.Add(new SigNormalParameter(sigType, null));
                }
                else
                    list.Add(new SigFnPtrParameter(sigType));
            }

            return (normal, varargs);

        }

        #region CorSigUncompress*

        internal CorHybridCallingConvention CorSigUncompressCallingConv() => SigBlobHelpers.CorSigUncompressCallingConv(ref currentSigBlob);
        internal int CorSigUncompressData() => SigBlobHelpers.CorSigUncompressData(ref currentSigBlob);

        internal int CorSigUncompressSignedInt()
        {
            var bytes = SigBlobHelpers.CorSigUncompressSignedInt(currentSigBlob, out var result);

            if (bytes > 0)
                currentSigBlob += bytes;

            return result;
        }

        internal CorElementType CorSigUncompressElementType() => SigBlobHelpers.CorSigUncompressElementType(ref currentSigBlob);
        internal mdToken CorSigUncompressToken() => SigBlobHelpers.CorSigUncompressToken(ref currentSigBlob);

        #endregion
    }
}
