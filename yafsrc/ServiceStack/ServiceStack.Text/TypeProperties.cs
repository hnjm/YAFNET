#if NET472 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using ServiceStack.Text;

#endif

namespace ServiceStack
{
    [Obsolete("Use TypeProperties<T>.Instance")]
    public static class TypeReflector<T> { }

    public class PropertyAccessor
    {
        public PropertyAccessor(
            PropertyInfo propertyInfo,
            GetMemberDelegate publicGetter,
            SetMemberDelegate publicSetter)
        {
            this.PropertyInfo = propertyInfo;
            this.PublicGetter = publicGetter;
            this.PublicSetter = publicSetter;
        }

        public PropertyInfo PropertyInfo { get; }

        public GetMemberDelegate PublicGetter { get; }

        public SetMemberDelegate PublicSetter { get; }
    }

    public class TypeProperties<T> : TypeProperties
    {
        public static readonly TypeProperties<T> Instance = new TypeProperties<T>();

        static TypeProperties()
        {
            Instance.Type = typeof(T);
            Instance.PublicPropertyInfos = typeof(T).GetPublicProperties();
            foreach (var pi in Instance.PublicPropertyInfos)
            {
                try
                {
                    Instance.PropertyMap[pi.Name] = new PropertyAccessor(
                        pi,
                        PclExport.Instance.CreateGetter(pi),
                        PclExport.Instance.CreateSetter(pi)
                    );
                }
                catch (Exception ex)
                {
                    Tracer.Instance.WriteError(ex);
                }
            }
        }

        public new static PropertyAccessor GetAccessor(string propertyName)
        {
            return Instance.PropertyMap.TryGetValue(propertyName, out var info)
                ? info
                : null;
        }
    }

    public abstract class TypeProperties
    {
        static Dictionary<Type, TypeProperties> CacheMap = new Dictionary<Type, TypeProperties>();

        public static Type FactoryType = typeof(TypeProperties<>);

        public static TypeProperties Get(Type type)
        {
            if (CacheMap.TryGetValue(type, out var value))
                return value;

            var genericType = FactoryType.MakeGenericType(type);
            var instanceFi = genericType.GetPublicStaticField("Instance");
            var instance = (TypeProperties)instanceFi.GetValue(null);

            Dictionary<Type, TypeProperties> snapshot, newCache;
            do
            {
                snapshot = CacheMap;
                newCache = new Dictionary<Type, TypeProperties>(CacheMap)
                {
                    [type] = instance
                };
            }
 while (!ReferenceEquals(
                Interlocked.CompareExchange(ref CacheMap, newCache, snapshot), snapshot));

            return instance;
        }

        public PropertyAccessor GetAccessor(string propertyName)
        {
            return this.PropertyMap.TryGetValue(propertyName, out var info)
                ? info
                : null;
        }

        public Type Type { get; protected set; }

        public readonly Dictionary<string, PropertyAccessor> PropertyMap =
            new Dictionary<string, PropertyAccessor>(PclExport.Instance.InvariantComparerIgnoreCase);

        public PropertyInfo[] PublicPropertyInfos { get; protected set; }

        public PropertyInfo GetPublicProperty(string name)
        {
            foreach (var pi in this.PublicPropertyInfos)
            {
                if (pi.Name == name)
                    return pi;
            }

            return null;
        }

        public GetMemberDelegate GetPublicGetter(PropertyInfo pi) => this.GetPublicGetter(pi?.Name);

        public GetMemberDelegate GetPublicGetter(string name)
        {
            if (name == null)
                return null;

            return this.PropertyMap.TryGetValue(name, out var info)
                ? info.PublicGetter
                : null;
        }

        public SetMemberDelegate GetPublicSetter(PropertyInfo pi) => this.GetPublicSetter(pi?.Name);

        public SetMemberDelegate GetPublicSetter(string name)
        {
            if (name == null)
                return null;

            return this.PropertyMap.TryGetValue(name, out var info)
                ? info.PublicSetter
                : null;
        }
    }

    public static class PropertyInvoker
    {
        [Obsolete("Use CreateSetter")]
        public static SetMemberDelegate GetPropertySetterFn(this PropertyInfo propertyInfo) =>
            PclExport.Instance.CreateSetter(propertyInfo);

        public static GetMemberDelegate CreateGetter(this PropertyInfo propertyInfo) =>
            PclExport.Instance.CreateGetter(propertyInfo);

        public static GetMemberDelegate<T> CreateGetter<T>(this PropertyInfo propertyInfo) =>
            PclExport.Instance.CreateGetter<T>(propertyInfo);

        public static SetMemberDelegate CreateSetter(this PropertyInfo propertyInfo) =>
            PclExport.Instance.CreateSetter(propertyInfo);

        public static SetMemberDelegate<T> CreateSetter<T>(this PropertyInfo propertyInfo) =>
            PclExport.Instance.CreateSetter<T>(propertyInfo);

        public static GetMemberDelegate GetReflection(PropertyInfo propertyInfo) => propertyInfo.GetValue;
        public static SetMemberDelegate SetReflection(PropertyInfo propertyInfo) => propertyInfo.SetValue;

        public static GetMemberDelegate<T> GetExpression<T>(PropertyInfo propertyInfo)
        {
            var expr = GetExpressionLambda<T>(propertyInfo);
            return expr.Compile();
        }

        public static Expression<GetMemberDelegate<T>> GetExpressionLambda<T>(PropertyInfo propertyInfo)
        {
            var instance = Expression.Parameter(typeof(T), "i");
            var property = typeof(T) != propertyInfo.DeclaringType
                ? Expression.Property(Expression.TypeAs(instance, propertyInfo.DeclaringType), propertyInfo)
                : Expression.Property(instance, propertyInfo);
            var convertProperty = Expression.TypeAs(property, typeof(object));
            return Expression.Lambda<GetMemberDelegate<T>>(convertProperty, instance);
        }

        public static GetMemberDelegate GetExpression(PropertyInfo propertyInfo)
        {
            var lambda = GetExpressionLambda(propertyInfo);
            var propertyGetFn = lambda.Compile();
            return propertyGetFn;
        }

        public static Expression<GetMemberDelegate> GetExpressionLambda(PropertyInfo propertyInfo)
        {
            var getMethodInfo = propertyInfo.GetGetMethod(nonPublic:true);
            if (getMethodInfo == null) return null;

            var oInstanceParam = Expression.Parameter(typeof(object), "oInstanceParam");
            var instanceParam = Expression.Convert(oInstanceParam, propertyInfo.ReflectedType); // propertyInfo.DeclaringType doesn't work on Proxy types

            var exprCallPropertyGetFn = Expression.Call(instanceParam, getMethodInfo);
            var oExprCallPropertyGetFn = Expression.Convert(exprCallPropertyGetFn, typeof(object));

            return Expression.Lambda<GetMemberDelegate>
            (
                oExprCallPropertyGetFn,
                oInstanceParam
            );
        }

        public static SetMemberDelegate<T> SetExpression<T>(PropertyInfo propertyInfo)
        {
            try
            {
                var lambda = SetExpressionLambda<T>(propertyInfo);
                return lambda?.Compile();
            }
            catch
            {
                // fallback for Android
                var mi = propertyInfo.GetSetMethod(nonPublic: true);
                return (o, convertedValue) =>
                    mi.Invoke(o, new[] { convertedValue });
            }
        }

        public static Expression<SetMemberDelegate<T>> SetExpressionLambda<T>(PropertyInfo propertyInfo)
        {
            var mi = propertyInfo.GetSetMethod(nonPublic: true);
            if (mi == null) return null;

            var instance = Expression.Parameter(typeof(T), "i");
            var argument = Expression.Parameter(typeof(object), "a");

            var instanceType = typeof(T) != propertyInfo.DeclaringType
                ? (Expression)Expression.TypeAs(instance, propertyInfo.DeclaringType)
                : instance;

            var setterCall = Expression.Call(
                instanceType,
                mi,
                Expression.Convert(argument, propertyInfo.PropertyType));

            return Expression.Lambda<SetMemberDelegate<T>>
            (
                setterCall, instance, argument
            );
        }

        public static SetMemberDelegate SetExpression(PropertyInfo propertyInfo)
        {
            var propertySetMethod = propertyInfo.GetSetMethod(nonPublic:true);
            if (propertySetMethod == null) return null;

            try
            {
                var instance = Expression.Parameter(typeof(object), "i");
                var argument = Expression.Parameter(typeof(object), "a");

                var instanceParam = Expression.Convert(instance, propertyInfo.ReflectedType);
                var valueParam = Expression.Convert(argument, propertyInfo.PropertyType);

                var setterCall = Expression.Call(instanceParam, propertySetMethod, valueParam);

                return Expression.Lambda<SetMemberDelegate>(setterCall, instance, argument).Compile();
            }
            catch
            {
                // fallback for Android
                return (o, convertedValue) =>
                    propertySetMethod.Invoke(o, new[] { convertedValue });
            }
        }

#if NET472 || NETSTANDARD2_0
        public static GetMemberDelegate<T> GetEmit<T>(PropertyInfo propertyInfo)
        {
            var getter = FieldInvoker.CreateDynamicGetMethod<T>(propertyInfo);

            var gen = getter.GetILGenerator();
            var mi = propertyInfo.GetGetMethod(true);

            if (typeof(T).IsValueType)
            {
                gen.Emit(OpCodes.Ldarga_S, 0);

                if (typeof(T) != propertyInfo.DeclaringType)
                {
                    gen.Emit(OpCodes.Unbox, propertyInfo.DeclaringType);
                }
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);

                if (typeof(T) != propertyInfo.DeclaringType)
                {
                    gen.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
                }
            }

            gen.Emit(mi.IsFinal ? OpCodes.Call : OpCodes.Callvirt, mi);

            if (propertyInfo.PropertyType.IsValueType)
            {
                gen.Emit(OpCodes.Box, propertyInfo.PropertyType);
            }

            gen.Emit(OpCodes.Isinst, typeof(object));

            gen.Emit(OpCodes.Ret);

            return (GetMemberDelegate<T>)getter.CreateDelegate(typeof(GetMemberDelegate<T>));
        }

        public static GetMemberDelegate GetEmit(PropertyInfo propertyInfo)
        {
            var getter = FieldInvoker.CreateDynamicGetMethod(propertyInfo);

            var gen = getter.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);

            gen.Emit(
                propertyInfo.DeclaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass,
                propertyInfo.DeclaringType);

            var mi = propertyInfo.GetGetMethod(true);
            gen.Emit(mi.IsFinal ? OpCodes.Call : OpCodes.Callvirt, mi);

            if (propertyInfo.PropertyType.IsValueType)
            {
                gen.Emit(OpCodes.Box, propertyInfo.PropertyType);
            }

            gen.Emit(OpCodes.Ret);

            return (GetMemberDelegate)getter.CreateDelegate(typeof(GetMemberDelegate));
        }

        public static SetMemberDelegate SetEmit(PropertyInfo propertyInfo)
        {
            var mi = propertyInfo.GetSetMethod(true);
            if (mi == null)
                return null;

            var setter = FieldInvoker.CreateDynamicSetMethod(propertyInfo);

            var gen = setter.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);

            gen.Emit(
                propertyInfo.DeclaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass,
                propertyInfo.DeclaringType);

            gen.Emit(OpCodes.Ldarg_1);

            gen.Emit(
                propertyInfo.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass,
                propertyInfo.PropertyType);

            gen.EmitCall(mi.IsFinal ? OpCodes.Call : OpCodes.Callvirt, mi, (Type[])null);

            gen.Emit(OpCodes.Ret);

            return (SetMemberDelegate)setter.CreateDelegate(typeof(SetMemberDelegate));
        }

#endif
    }
}
