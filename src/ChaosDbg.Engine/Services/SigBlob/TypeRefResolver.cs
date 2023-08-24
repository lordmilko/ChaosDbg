using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg
{
    public interface ITypeRefResolver
    {
        ResolvedTypeRef ResolveTypeRef(mdTypeRef typeRef, MetaDataImport import);

        ResolvedMemberRef ResolveMemberRef(mdMemberRef memberRef, MetaDataImport import);
    }

    class TypeRefResolver : ITypeRefResolver
    {
        private Dictionary<IMetaDataImport, ModuleResolutionContext> mdiToCtxCache = new Dictionary<IMetaDataImport, ModuleResolutionContext>();
        private Dictionary<string, MetaDataImport> asmCache = new Dictionary<string, MetaDataImport>();

        public ResolvedTypeRef ResolveTypeRef(mdTypeRef typeRef, MetaDataImport import)
        {
            if (!mdiToCtxCache.TryGetValue(import.Raw, out var moduleCtx))
            {
                moduleCtx = new ModuleResolutionContext(import);
                mdiToCtxCache[import.Raw] = moduleCtx;
            }
            else
            {
                if (moduleCtx.ResolvedTypeRefs.TryGetValue(typeRef, out var resolved))
                    return resolved;
            }

            var props = import.GetTypeRefProps(typeRef);

            var resolutionScopeType = props.ptkResolutionScope.Type;

            ResolvedTypeRef resolvedTypeRef;

            switch (resolutionScopeType)
            {
                case CorTokenType.mdtAssemblyRef:
                    resolvedTypeRef = ResolveFromAssemblyTypeRef((mdAssemblyRef)props.ptkResolutionScope, typeRef, props.szName, moduleCtx);
                    break;

                case CorTokenType.mdtTypeRef:
                    resolvedTypeRef = ResolveFromNestedTypeRef(typeRef, import);
                    break;

                case CorTokenType.mdtModule:
                    resolvedTypeRef = ResolveFromModuleTypeRef((mdModule)props.ptkResolutionScope, typeRef, props.szName, moduleCtx);
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle token of type {resolutionScopeType}");
            }

            moduleCtx.ResolvedTypeRefs[typeRef] = resolvedTypeRef;

            return resolvedTypeRef;
        }

        private ResolvedTypeRef ResolveFromAssemblyTypeRef(
            mdAssemblyRef assemblyRef,
            mdTypeRef typeRef,
            string typeName,
            ModuleResolutionContext typeRefCtx)
        {
            var mdai = typeRefCtx.Import.As<MetaDataAssemblyImport>();

            var asmRefProps = mdai.GetAssemblyRefProps(assemblyRef);

            if (!asmCache.TryGetValue(asmRefProps.szName, out var typeDefMDI))
            {
                if (!ShouldResolveAssemblyRef(typeRef, asmRefProps.szName, typeName))
                    return null;

                if (asmRefProps.szName == "netstandard")
                {
                    var path = typeof(object).Assembly.Location;

                    var disp = new MetaDataDispenserEx();

                    typeDefMDI = disp.OpenScope<MetaDataImport>(path, CorOpenFlags.ofReadOnly);

                    asmCache[asmRefProps.szName] = typeDefMDI;
                }
                else
                    throw new NotImplementedException($"Don't know how to resolve type from assembly {asmRefProps.szName}");
            }

            if (!mdiToCtxCache.TryGetValue(typeDefMDI.Raw, out var typeDefCtx))
            {
                typeDefCtx = new ModuleResolutionContext(typeDefMDI);
                mdiToCtxCache[typeDefMDI.Raw] = typeDefCtx;
            }

            //Some types are .NET Standard exclusive, e.g. System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute.
            //In these cases, return null
            if (typeDefMDI.TryFindTypeDefByName(typeName, mdToken.Nil, out var typeDef) != HRESULT.S_OK)
                return null;

            return new ResolvedTypeRef(
                typeRef,
                typeDef,
                typeRefCtx,
                typeDefCtx
            );
        }

        private ResolvedTypeRef ResolveFromNestedTypeRef(mdTypeRef typeRef, MetaDataImport import)
        {
            throw new NotImplementedException("Processing nested types is not implemented");
        }

        private ResolvedTypeRef ResolveFromModuleTypeRef(mdModule module, mdTypeRef typeRef, string typeName, ModuleResolutionContext moduleRefCtx)
        {
            if (module != moduleRefCtx.Import.ModuleFromScope)
                throw new NotImplementedException("Don't know how to handle a module that is not the current module");

            //We have a typeref to a type within this module

            var typeDef = moduleRefCtx.Import.FindTypeDefByName(typeName, mdToken.Nil);

            return new ResolvedTypeRef(
                typeRef,
                typeDef,
                moduleRefCtx,
                moduleRefCtx //This is currently predicated on the assumption there'll only be one module, thus the source and destination modules will be the same
            );
        }

        public ResolvedMemberRef ResolveMemberRef(mdMemberRef memberRef, MetaDataImport import)
        {
            if (!mdiToCtxCache.TryGetValue(import.Raw, out var moduleCtx))
            {
                moduleCtx = new ModuleResolutionContext(import);
                mdiToCtxCache[import.Raw] = moduleCtx;
            }
            else
            {
                if (moduleCtx.ResolvedMemberRefs.TryGetValue(memberRef, out var resolved))
                    return resolved;
            }

            var memberProps = import.GetMemberRefProps(memberRef);

            ResolvedMemberRef resolvedMemberRef;

            switch (memberProps.ptk.Type)
            {
                case CorTokenType.mdtTypeDef:
                    throw new NotImplementedException("Resolving an mdMemberRef from a typedef is not implemented");

                case CorTokenType.mdtTypeRef:
                    resolvedMemberRef = ResolveMemberRefFromTypeRef(memberRef, memberProps, moduleCtx);
                    break;

                case CorTokenType.mdtTypeSpec:
                    throw new NotImplementedException("Resolving an mdMemberRef from a typespec is not implemented");

                default:
                    throw new NotImplementedException($"Don't know how to resolve an mdMemberRef from a token of type {memberProps.ptk.Type}.");
            }

            moduleCtx.ResolvedMemberRefs[memberRef] = resolvedMemberRef;
            return resolvedMemberRef;
        }

        private ResolvedMemberRef ResolveMemberRefFromTypeRef(mdMemberRef memberRef, GetMemberRefPropsResult props, ModuleResolutionContext memberRefCtx)
        {
            var resolvedTypeRef = ResolveTypeRef((mdTypeRef)props.ptk, memberRefCtx.Import);

            if (resolvedTypeRef == null)
                return null;

            var blob = props.ppvSigBlob;

            //Since we have the sigblob of the member, we can inspect the start of
            //the blob to get the calling convention, which can help tell us whether it's
            //a method or a field
            var conv = SigBlobHelpers.CorSigUncompressCallingConv(ref blob);

            if (conv == CorHybridCallingConvention.FIELD)
                throw new NotImplementedException("Don't know how to resolve an mdMemberRef pointing to a field");

            //Must be a method then. Methods can be overloaded, but we don't support that
            var methodImport = resolvedTypeRef.TypeDefModule.Import;

            var candidates = methodImport.EnumMethods(resolvedTypeRef.TypeDef)
                .Select(v => new
                {
                    Token = v,
                    Props = methodImport.GetMethodProps(v)
                })
                .Where(v => v.Props.szMethod == props.szMember)
                .ToArray();

            mdMethodDef match;

            if (candidates.Length == 0)
                throw new NotImplementedException($"Failed to find any methods matching mdMemberRef {props.szMember}");

            if (candidates.Length == 1)
                match = candidates[0].Token;
            else
            {
                //There's multiple possible overloads. Let's have a look at the MemberRef SigBlob
                var sig = new SigReaderInternal(props.ppvSigBlob, props.pbSig, memberRef, memberRefCtx.Import).ParseMethod(props.szMember, true);

                candidates = candidates.Where(c => methodImport.EnumParams(c.Token).Length == sig.Parameters.Length).ToArray();

                if (candidates.Length == 1)
                    match = candidates[0].Token;
                else
                    throw new NotImplementedException($"Failed to disambiguate mdMemberRef {props.szMember}. After analyzing the number of expected parameters, had {candidates.Length} candidate method overloads remaining");
            }

            return new ResolvedMemberRef(
                memberRef,
                match,
                memberRefCtx,
                resolvedTypeRef.TypeDefModule
            );
        }

        protected virtual bool ShouldResolveAssemblyRef(mdTypeRef typeRef, string assemblyRef, string typeName)
        {
            return true;
        }
    }
}
