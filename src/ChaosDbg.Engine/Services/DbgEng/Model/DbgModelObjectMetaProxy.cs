using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ChaosLib.Dynamic;
using ClrDebug;
using ClrDebug.DbgEng;
using Microsoft.CSharp.RuntimeBinder;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a <see cref="DynamicMetaProxy{TInstance}"/> capable of interacting with the members of an underlying <see cref="ModelObject"/>.<para/>
    /// An instance of this type is passed to a <see cref="DynamicMetaObject{TInstance}"/> returned from an outer <see cref="DynamicDbgModelObject"/> in response
    /// to a request for dynamic binding.
    /// </summary>
    class DbgModelObjectMetaProxy : DynamicMetaProxy<DynamicDbgModelObject>
    {
        public override bool TryConvert(DynamicDbgModelObject instance, ConvertBinder binder, out object value)
        {
            if (!binder.Type.IsGenericType && binder.Type != typeof(IEnumerable))
            {
                if (binder.Type.IsPrimitive)
                {
                    var raw = instance.Value.IntrinsicValue;

                    if (raw != null)
                    {
                        if (raw.GetType() == binder.Type)
                        {
                            value = raw;
                            return true;
                        }

                        if (raw is IConvertible c)
                        {
                            value = c.ToType(binder.Type, null);
                            return true;
                        }
                    }
                }
                else if (binder.Type == typeof(string))
                {
                    value = instance.Value.IntrinsicValue;
                    return true;
                }    
                else if (binder.Type.IsInterface)
                    throw new NotImplementedException("Converting to an interface is not implemeneted");
            }
            else
            {
                var underlying = Nullable.GetUnderlyingType(binder.Type);

                if (underlying != null)
                {
                    if (instance.Value.Kind == ModelObjectKind.ObjectNoValue)
                    {
                        value = null;
                        return true;
                    }

                    throw new NotImplementedException("Converting to a nullable value is not implemented");
                }
            }

            value = false;
            return false;
        }

        public override bool TryGetMember(DynamicDbgModelObject instance, GetMemberBinder binder, out object value)
        {
            if (instance.Value.TryGetKeyValue(binder.Name, out var result) == HRESULT.S_OK)
            {
                value = DynamicDbgModelObject.New(result.@object, result.metadata, instance.DataModelManager);
                return true;
            }

            value = false;
            return false;
        }

        public override bool TryInvokeMember(DynamicDbgModelObject instance, InvokeMemberBinder binder, object[] args, out object value)
        {
            //Does our underlying value implement IModelMethod?
            if (instance.Value.TryGetKeyValue(binder.Name, out var functionModel) == HRESULT.S_OK &&
                functionModel.@object.TryGetIntrinsicValue(out var intrinsic) == HRESULT.S_OK &&
                intrinsic is IModelMethod m)
            {
                var method = new ModelMethod(m);

                //Construct the arguments to pass to the function
                var modelArgs = GetInvocationArgs(instance, args);

                var result = method.Call(functionModel.@object.Raw, args.Length, modelArgs);

                //If we succeeded, we'll get back an iterable object full of DbgEng style LINQ methods.
                //We don't want to use those, so just create an EnumerableModelObjectProxy capable of iterating through
                //all the elements
                value = DynamicDbgModelObject.New(result.ppResult, result.ppMetadata, instance.DataModelManager);
                return true;
            }

            value = null;
            return false;
        }

        public override bool TryGetIndex(DynamicDbgModelObject instance, GetIndexBinder binder, object[] indexes, out object value)
        {
            var indexable = instance.Value.Concept.Indexable;

            if (indexable != null)
            {
                var modelArgs = GetInvocationArgs(instance, indexes);

                if (indexable.conceptInterface.TryGetAt(instance.Value.Raw, modelArgs.Length, modelArgs, out var result) == HRESULT.S_OK)
                {
                    value = DynamicDbgModelObject.New(result.@object, result.metadata, instance.DataModelManager);
                    return true;
                }
            }

            return base.TryGetIndex(instance, binder, indexes, out value);
        }

        public override bool TryBinaryOperation(DynamicDbgModelObject instance, BinaryOperationBinder binder, object arg, out object value)
        {
            var argType = arg.GetType();

            object Convert()
            {
                var site = CallSite<Func<CallSite, object, object>>.Create(
                    Binder.Convert(0, argType, typeof(DynamicDbgModelObject))
                );

                var result = site.Target(site, instance);

                return result;
            }

            if (argType == typeof(string))
            {
                switch (binder.Operation)
                {
                    case ExpressionType.Equal:
                    {
                        var left = (string) Convert();

                        value = (string) arg == left;
                        return true;
                    }
                }
            }

            value = false;
            return false;
        }

        private IModelObject[] GetInvocationArgs(DynamicDbgModelObject instance, object[] args)
        {
            if (args.Length == 1 && args[0].GetType().IsArray)
                return GetInvocationArgs(instance, (object[]) args[0]);

            var modelArgs = new IModelObject[args.Length];

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg is ModelObject mo)
                    modelArgs[i] = mo.Raw;
                else if (arg is IModelObject imo)
                    modelArgs[i] = imo;
                else
                {
                    //Whatever it is, wrap it up and hope for the best!
                    modelArgs[i] = instance.DataModelManager.CreateIntrinsicObject(ModelObjectKind.ObjectIntrinsic, args[i]).Raw;
                }
            }

            return modelArgs;
        }

        public override IEnumerable<string> GetDynamicMemberNames(DynamicDbgModelObject instance)
        {
            var keys = instance.Value.Keys;

            return keys.Select(v => v.key).ToArray();
        }
    }
}
