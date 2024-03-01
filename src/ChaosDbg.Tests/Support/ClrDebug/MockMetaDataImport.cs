using System;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Tests
{
    class MockMetaDataImport : IMetaDataImport, IMetaDataAssemblyImport
    {
        #region IMetaDataImport

        public void CloseEnum(IntPtr hEnum)
        {
            throw new NotImplementedException();
        }

        public HRESULT CountEnum(IntPtr hEnum, out int pulCount)
        {
            throw new NotImplementedException();
        }

        public HRESULT ResetEnum(IntPtr hEnum, int ulPos)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumTypeDefs(ref IntPtr phEnum, mdTypeDef[] typeDefs, int cMax, out int pcTypeDefs)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumInterfaceImpls(ref IntPtr phEnum, mdTypeDef td, mdInterfaceImpl[] rImpls, int cMax, out int pcImpls)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumTypeRefs(ref IntPtr phEnum, mdTypeRef[] rTypeRefs, int cMax, out int pcTypeRefs)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindTypeDefByName(string szTypeDef, mdToken tkEnclosingClass, out mdTypeDef typeDef)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetScopeProps(char[] szName, int cchName, out int pchName, out Guid pmvid)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetModuleFromScope(out mdModule pmd)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetTypeDefProps(
            mdTypeDef td,
            char[] szTypeDef,
            int cchTypeDef,
            out int pchTypeDef,
            out CorTypeAttr pdwTypeDefFlags,
            out mdToken ptkExtends)
        {
            pchTypeDef = 2;
            pdwTypeDefFlags = default;
            ptkExtends = default;

            if (szTypeDef == null)
                return S_FALSE;

            szTypeDef[0] = 'A';
            szTypeDef[1] = '\0';

            return S_OK;
        }

        public HRESULT GetInterfaceImplProps(mdInterfaceImpl iiImpl, out mdTypeDef pClass, out mdToken ptkIface)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetTypeRefProps(mdTypeRef tr, out mdToken ptkResolutionScope, char[] szName, int cchName, out int pchName)
        {
            throw new NotImplementedException();
        }

        public HRESULT ResolveTypeRef(mdTypeRef tr, Guid riid, out object ppIScope, out mdTypeDef ptd)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMembers(ref IntPtr phEnum, mdTypeDef cl, mdToken[] rMembers, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMembersWithName(ref IntPtr phEnum, mdTypeDef cl, string szName, mdToken[] rMembers, int cMax,
            out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMethods(ref IntPtr phEnum, mdTypeDef cl, mdMethodDef[] rMethods, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMethodsWithName(ref IntPtr phEnum, mdTypeDef cl, string szName, mdMethodDef[] rMethods, int cMax,
            out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumFields(ref IntPtr phEnum, mdTypeDef cl, mdFieldDef[] rFields, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumFieldsWithName(ref IntPtr phEnum, mdTypeDef cl, string szName, mdFieldDef[] rFields, int cMax,
            out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumParams(ref IntPtr phEnum, mdMethodDef mb, mdParamDef[] rParams, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMemberRefs(ref IntPtr phEnum, mdToken tkParent, mdMemberRef[] rMemberRefs, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMethodImpls(ref IntPtr phEnum, mdTypeDef td, mdToken[] rMethodBody, mdToken[] rMethodDecl, int cMax,
            out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumPermissionSets(ref IntPtr phEnum, mdToken tk, CorDeclSecurity dwActions, mdPermission[] rPermission,
            int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindMember(mdToken td, string szName, IntPtr pvSigBlob, int cbSigBlob, out mdToken pmb)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindMethod(mdToken td, string szName, IntPtr pvSigBlob, int cbSigBlob, out mdMethodDef pmb)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindField(mdToken td, string szName, IntPtr pvSigBlob, int cbSigBlob, out mdFieldDef pmb)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindMemberRef(mdToken td, string szName, IntPtr pvSigBlob, int cbSigBlob, out mdMemberRef pmr)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetMethodProps(
            mdMethodDef mb,
            out mdTypeDef pClass,
            char[] szMethod,
            int cchMethod,
            out int pchMethod,
            out CorMethodAttr pdwAttr,
            out IntPtr ppvSigBlob,
            out int pcbSigBlob,
            out int pulCodeRVA,
            out CorMethodImpl pdwImplFlags)
        {
            pClass = default;
            pchMethod = 2;
            pdwAttr = default;
            ppvSigBlob = default;
            pcbSigBlob = default;
            pulCodeRVA = default;
            pdwImplFlags = default;

            if (szMethod == null)
                return S_FALSE;
            else
            {
                szMethod[0] = 'A';
                szMethod[1] = '\0';
                return S_OK;
            }
        }

        public HRESULT GetMemberRefProps(
            mdMemberRef mr,
            out mdToken ptk,
            char[] szMember,
            int cchMember,
            out int pchMember,
            out IntPtr ppvSigBlob,
            out int pbSig)
        {
            ptk = default;
            pchMember = 2;
            ppvSigBlob = default;
            pbSig = default;

            if (szMember == null)
                return S_FALSE;

            szMember[0] = 'A';
            szMember[1] = '\0';
            ptk = Extensions.TokenFromRid(1, CorTokenType.mdtMethodDef);

            return S_OK;
        }

        public HRESULT EnumProperties(ref IntPtr phEnum, mdTypeDef td, mdProperty[] rProperties, int cMax, out int pcProperties)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumEvents(ref IntPtr phEnum, mdTypeDef td, mdEvent[] rEvents, int cMax, out int pcEvents)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetEventProps(mdEvent ev, out mdTypeDef pClass, char[] szEvent, int cchEvent, out int pchEvent,
            out CorEventAttr pdwEventFlags, out mdToken ptkEventType, out mdMethodDef pmdAddOn, out mdMethodDef pmdRemoveOn,
            out mdMethodDef pmdFire, mdMethodDef[] rmdOtherMethod, int cMax, out int pcOtherMethod)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumMethodSemantics(ref IntPtr phEnum, mdMethodDef mb, mdToken[] rEventProp, int cMax, out int pcEventProp)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetMethodSemantics(mdMethodDef mb, mdToken tkEventProp, out CorMethodSemanticsAttr pdwSemanticsFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetClassLayout(mdTypeDef td, out int pdwPackSize, COR_FIELD_OFFSET[] rFieldOffset, int cMax,
            out int pcFieldOffset, out int pulClassSize)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetFieldMarshal(mdToken tk, out IntPtr ppvNativeType, out int pcbNativeType)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetRVA(mdToken tk, out int pulCodeRVA, out CorMethodImpl pdwImplFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetPermissionSetProps(mdPermission pm, out CorDeclSecurity pdwAction, out IntPtr ppvPermission,
            out int pcbPermission)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetSigFromToken(mdSignature mdSig, out IntPtr ppvSig, out int pcbSig)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetModuleRefProps(mdModuleRef mur, char[] szName, int cchName, out int pchName)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumModuleRefs(ref IntPtr phEnum, mdModuleRef[] rModuleRefs, int cmax, out int pcModuleRefs)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetTypeSpecFromToken(mdTypeSpec typespec, out IntPtr ppvSig, out int pcbSig)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetNameFromToken(mdToken tk, out IntPtr pszUtf8NamePtr)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumUnresolvedMethods(ref IntPtr phEnum, mdToken[] rMethods, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetUserString(
            mdString stk,
            char[] szString,
            int cchString,
            out int pchString)
        {
            pchString = 2;

            if (szString == null)
                return S_FALSE;

            szString[0] = 'A';
            szString[1] = '\0';

            return S_OK;
        }

        public HRESULT GetPinvokeMap(mdToken tk, out CorPinvokeMap pdwMappingFlags, char[] szImportName, int cchImportName,
            out int pchImportName, out mdModuleRef pmrImportDLL)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumSignatures(ref IntPtr phEnum, mdSignature[] rSignatures, int cmax, out int pcSignatures)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumTypeSpecs(ref IntPtr phEnum, mdTypeSpec[] rTypeSpecs, int cmax, out int pcTypeSpecs)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumUserStrings(ref IntPtr phEnum, mdString[] rStrings, int cmax, out int pcStrings)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetParamForMethodIndex(mdMethodDef md, int ulParamSeq, out mdParamDef ppd)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumCustomAttributes(ref IntPtr phEnum, mdToken tk, mdToken tkType, mdCustomAttribute[] rCustomAttributes,
            int cMax, out int pcCustomAttributes)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetCustomAttributeProps(mdCustomAttribute cv, out mdToken ptkObj, out mdToken ptkType, out IntPtr ppBlob,
            out int pcbSize)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindTypeRef(mdToken tkResolutionScope, string szName, out mdTypeRef ptr)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetMemberProps(mdToken mb, out mdTypeDef pClass, char[] szMember, int cchMember, out int pchMember,
            out int pdwAttr, out IntPtr ppvSigBlob, out int pcbSigBlob, out int pulCodeRVA, out int pdwImplFlags,
            out CorElementType pdwCPlusTypeFlag, out IntPtr ppValue, out int pcchValue)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetFieldProps(
            mdFieldDef mb,
            out mdTypeDef pClass,
            char[] szField,
            int cchField,
            out int pchField,
            out CorFieldAttr pdwAttr,
            out IntPtr ppvSigBlob,
            out int pcbSigBlob,
            out CorElementType pdwCPlusTypeFlag,
            out IntPtr ppValue,
            out int pcchValue)
        {
            pClass = default;
            pchField = 2;
            pdwAttr = default;
            ppvSigBlob = default;
            pcbSigBlob = default;
            pdwCPlusTypeFlag = default;
            ppValue = default;
            pcchValue = default;

            if (szField == null)
                return S_FALSE;

            szField[0] = 'A';
            szField[1] = '\0';

            return S_OK;
        }

        public HRESULT GetPropertyProps(mdProperty prop, out mdTypeDef pClass, char[] szProperty, int cchProperty, out int pchProperty,
            out CorPropertyAttr pdwPropFlags, out IntPtr ppvSig, out int pbSig, out CorElementType pdwCPlusTypeFlag,
            out IntPtr ppDefaultValue, out int pcchDefaultValue, out mdMethodDef pmdSetter, out mdMethodDef pmdGetter,
            mdMethodDef[] rmdOtherMethod, int cMax, out int pcOtherMethod)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetParamProps(mdParamDef tk, out mdMethodDef pmd, out int pulSequence, char[] szName, int cchName,
            out int pchName, out CorParamAttr pdwAttr, out CorElementType pdwCPlusTypeFlag, out IntPtr ppValue,
            out IntPtr pcchValue)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetCustomAttributeByName(mdToken tkObj, string szName, out IntPtr ppData, out int pcbData)
        {
            throw new NotImplementedException();
        }

        public bool IsValidToken(mdToken tk)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetNestedClassProps(mdTypeDef tdNestedClass, out mdTypeDef ptdEnclosingClass)
        {
            ptdEnclosingClass = default;
            return E_NOTIMPL;
        }

        public HRESULT GetNativeCallConvFromSig(IntPtr pvSig, int cbSig, out int pCallConv)
        {
            throw new NotImplementedException();
        }

        public HRESULT IsGlobal(mdToken pd, out bool pbGlobal)
        {
            throw new NotImplementedException();
        }

        #endregion
        #region IMetaDataAssemblyImport

        public HRESULT GetAssemblyProps(mdAssembly mda, out IntPtr ppbPublicKey, out int pcbPublicKey, out int pulHashAlgId,
            char[] szName, int cchName, out int pchName, out ASSEMBLYMETADATA pMetaData, out CorAssemblyFlags pdwAssemblyFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetAssemblyRefProps(mdAssemblyRef mdar, out IntPtr ppbPublicKeyOrToken, out int pcbPublicKeyOrToken,
            char[] szName, int cchName, out int pchName, out ASSEMBLYMETADATA pMetaData, out IntPtr ppbHashValue,
            out int pcbHashValue, out CorAssemblyFlags pdwAssemblyFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetFileProps(mdFile mdf, char[] szName, int cchName, out int pchName, out IntPtr ppbHashValue,
            out int pcbHashValue, out CorFileFlags pdwFileFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetExportedTypeProps(mdExportedType mdct, char[] szName, int cchName, out int pchName,
            out mdToken ptkImplementation, out mdTypeDef ptkTypeDef, out CorTypeAttr pdwExportedTypeFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetManifestResourceProps(mdManifestResource mdmr, char[] szName, int cchName, out int pchName,
            out mdToken ptkImplementation, out int pdwOffset, out CorManifestResourceFlags pdwResourceFlags)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumAssemblyRefs(ref IntPtr phEnum, mdAssemblyRef[] rAssemblyRefs, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumFiles(ref IntPtr phEnum, mdFile[] rFiles, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumExportedTypes(ref IntPtr phEnum, mdExportedType[] rExportedTypes, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumManifestResources(ref IntPtr phEnum, mdManifestResource[] rManifestResources, int cMax, out int pcTokens)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetAssemblyFromScope(out mdAssembly ptkAssembly)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindExportedTypeByName(string szName, mdToken mdtExportedType, out mdExportedType mdExportedType)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindManifestResourceByName(string szName, out mdManifestResource ptkManifestResource)
        {
            throw new NotImplementedException();
        }

        public HRESULT FindAssembliesByName(string szAppBase, string szPrivateBin, string szAssemblyName, object[] ppIUnk, int cMax,
            out int pcAssemblies)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
