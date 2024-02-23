using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class DelegateHelper
{
    public static FieldInfo GetFieldInfo(Type source, string fieldName)
    {
        var fieldInfo = (source.GetField(fieldName) ??
                              source.GetField(fieldName, BindingFlags.Instance |
                              BindingFlags.NonPublic)) ??
                              source.GetField(fieldName, BindingFlags.Instance |
                              BindingFlags.NonPublic | BindingFlags.Public);
        return fieldInfo;
    }

    public static bool EventLinker<T, Y>(T listener, Y speaker, string eventName, string eventHandler)
    {
        EventInfo eInfo = typeof(Y).GetEvent(eventName);
        Type handlerType = eInfo.EventHandlerType;

        if (eInfo != null && handlerType != null)
        {
            Delegate d = Delegate.CreateDelegate(handlerType, listener, eventHandler, false, false);
            if (d != null)
            {
                eInfo.AddEventHandler(speaker, d);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Bind the event of name eventName of the speaker object to the target object with eventHandlerInfo method
    /// </summary>
    /// <param name="speaker"></param>
    /// <param name="eventName"></param>
    /// <param name="target"></param>
    /// <param name="eventHandlerInfo"></param>
    /// <returns></returns>
    public static bool EventLinker(object speaker, string eventName, object target, MethodInfo eventHandlerInfo)
    {
        EventInfo eventInfo = speaker.GetType().GetEvent(eventName);

        if (eventInfo == null)
        {
            Debug.LogError("Event Not Found");
            return false;
        }

        Type handlerType = eventInfo.EventHandlerType;

        if (handlerType == null)
        {
            Debug.LogError("Handler type mismatch");
            return false;
        }

        var listennerHandlerMethodParametersInfo = eventHandlerInfo.GetParameters();
        var eventParameters = handlerType.GetMethod("Invoke").GetParameters();

        if (listennerHandlerMethodParametersInfo.Length != eventParameters.Length)
        {
            Debug.LogError("Parameters count mismatch");
            return false;
        }

        for (int i = 0; i < listennerHandlerMethodParametersInfo.Length; ++i)
        {
            if (listennerHandlerMethodParametersInfo[i].ParameterType != eventParameters[i].ParameterType)
            {
                Debug.LogError("Parameters type mismatch");

                return false;
            }
        }

        Delegate eventDelegate = Delegate.CreateDelegate(handlerType, target, eventHandlerInfo.Name, false, false);
        if (eventDelegate != null)
        {
            eventInfo.AddEventHandler(speaker, eventDelegate);
            return true;

            /*
             * Autre Methode
             * 
                MethodInfo addHandler = eventInfo.GetAddMethod();
                object[] handlerArguments = { eventDelegate };
                addHandler.Invoke(speaker, handlerArguments);
             */
        }
        return false;
    }

    /// <summary>
    /// Returns a generic action delegate for the MethodInfo (static only methods)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Action<T> CreateMethodAction<T>(MethodInfo methodInfo)
    {
        var obj = Expression.Parameter(typeof(T), "obj");
        var convert = Expression.Convert(obj, methodInfo.GetParameters().First().ParameterType);
        var call = Expression.Call(methodInfo, convert);
        var lambda = Expression.Lambda<Action<T>>(call, obj);

        return lambda.Compile();
    }

    /// <summary>
    /// Returns a generic action delegate for the MethodInfo (instance only methods)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Action<T> CreateInstanceMethodAction<T>(object instance, MethodInfo methodInfo)
    {
        var obj = Expression.Parameter(typeof(T), "obj");
        var convert = Expression.Convert(obj, methodInfo.GetParameters().First().ParameterType);
        var call = Expression.Call(Expression.Constant(instance), methodInfo, convert);
        var lambda = Expression.Lambda<Action<T>>(call, obj);

        return lambda.Compile();
    }

    public static Func<T> CreateInstanceMethodFunctionDelegate<T>(object instance, MethodInfo methodInfo)
    {
        return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), instance, methodInfo.Name, true);
    }

    public static Func<T1, T2> CreateInstanceMethodFunctionDelegate<T1, T2>(object instance, MethodInfo methodInfo)
    {
        /*var obj = Expression.Parameter(typeof(T1), "obj");
        var convert = Expression.Convert(obj, methodInfo.GetParameters().First().ParameterType);
        var call = Expression.Call(Expression.Constant(instance), methodInfo, convert);
        var lambda = Expression.Lambda<Func<T1, T2>>(call, obj);*/

        return (Func<T1, T2>)Delegate.CreateDelegate(typeof(Func<T1, T2>), instance, methodInfo.Name, true);
    }

    public static Func<T1, T2, T3> CreateInstanceMethodFunctionDelegate<T1, T2, T3>(object instance, MethodInfo methodInfo)
    {
        /*var obj = Expression.Parameter(typeof(T1), "obj");
        var convert = Expression.Convert(obj, methodInfo.GetParameters().First().ParameterType);
        var call = Expression.Call(Expression.Constant(instance), methodInfo, convert);
        var lambda = Expression.Lambda<Func<T1, T2>>(call, obj);*/

        return (Func<T1, T2, T3>)Delegate.CreateDelegate(typeof(Func<T1, T2, T3>), instance, methodInfo.Name, true);
    }

    /// <summary>
    /// Returns a dynamic action delegate with no parameter on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Action CreateInstanceMethodActionDynamic(object instance, MethodInfo methodInfo)
    {
        var call = Expression.Call(Expression.Constant(instance), methodInfo);
        var lambda = Expression.Lambda<Action>(call);

        return lambda.Compile();
    }

    /// <summary>
    /// Returns a dynamic action delegate with one parameter on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Action<object> CretaInstanceMethodActionDynamic_1(object instance, MethodInfo methodInfo)
    {
        var obj = Expression.Parameter(typeof(object), "obj");
        var convert = Expression.Convert(obj, methodInfo.GetParameters().First().ParameterType);
        var call = Expression.Call(Expression.Constant(instance), methodInfo, convert);
        var lambda = Expression.Lambda<Action<object>>(call, obj);

        return lambda.Compile();
    }

    /// <summary>
    /// Returns a dynamic action delegate with two parameters on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Action<object, object> CreateInstanceMethodActionDynamic_2(object instance, MethodInfo methodInfo)
    {
        var parames = methodInfo.GetParameters();
        var obj1 = Expression.Parameter(typeof(object), "obj");
        var convert1 = Expression.Convert(obj1, parames[0].ParameterType);

        var obj2 = Expression.Parameter(typeof(object), "obj2");
        var convert2 = Expression.Convert(obj2, parames[1].ParameterType);

        var call = Expression.Call(Expression.Constant(instance), methodInfo, convert1, convert2);
        var lambda = Expression.Lambda<Action<object, object>>(call, obj1, obj2);

        return lambda.Compile();
    }

    public static Action<object, object, object> CreateInstanceMethodActionDynamic_3(object instance, MethodInfo methodInfo)
    {
        ParameterExpression[] expressions = null;
        UnaryExpression[] converts = null;

        GetInstanceActionDynamicParameterDatas(instance, methodInfo, out expressions, out converts);

        var call = Expression.Call(Expression.Constant(instance), methodInfo, converts);
        var lambda = Expression.Lambda<Action<object, object, object>>(call, expressions);

        return lambda.Compile();
    }

    public static Action<object, object, object, object> CreateInstanceMethodActionDynamic_4(object instance, MethodInfo methodInfo)
    {
        ParameterExpression[] expressions = null;
        UnaryExpression[] converts = null;

        GetInstanceActionDynamicParameterDatas(instance, methodInfo, out expressions, out converts);

        var call = Expression.Call(Expression.Constant(instance), methodInfo, converts);
        var lambda = Expression.Lambda<Action<object, object, object, object>>(call, expressions);

        return lambda.Compile();
    }

    /// <summary>
    /// Returns a dynamic action delegate with two parameters on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static void GetInstanceActionDynamicParameterDatas(object instance, MethodInfo methodInfo, out ParameterExpression[] parameterExpressions, out UnaryExpression[] unaryExpressionConverts)
    {
        var methodParameters = methodInfo.GetParameters();

        ParameterExpression[] expressions = new ParameterExpression[methodParameters.Length];
        UnaryExpression[] converts = new UnaryExpression[methodParameters.Length];
        for (int i = 0; i < methodParameters.Length; ++i)
        {
            expressions[i] = Expression.Parameter(typeof(object), "obj_" + i);
            converts[i] = Expression.Convert(expressions[i], methodParameters[0].ParameterType);
        }
        parameterExpressions = expressions;
        unaryExpressionConverts = converts;
        /* var call = Expression.Call(Expression.Constant(instance), methodInfo, converts);

         var lambda = Expression.Lambda<Action>(call, expressions);
         return lambda.Compile();*/
    }

    /// <summary>
    /// Returns a generic delegate with one parameter on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Delegate CreateActionMethodDelegate<T>(object instance, MethodInfo methodInfo)
    {
        Delegate del = Delegate.CreateDelegate(typeof(Action<T>), instance, methodInfo.Name, false, true);
        return del;
    }

    /// <summary>
    /// Returns a generic delegate with two parameter on the instance object for the MethodInfo (instance only methods)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    public static Delegate CreateActionMethodDelegate<T1, T2>(object instance, MethodInfo methodInfo)
    {
        Delegate del = Delegate.CreateDelegate(typeof(Action<T1, T2>), instance, methodInfo.Name, false, true);
        return del;
    }

    public static Action<object, T> CreateLambdaFieldSetter<T>(Type source, string fieldName)
    {
        var fieldInfo = GetFieldInfo(source, fieldName);
        if (fieldInfo != null)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            var valueParam = Expression.Parameter(typeof(object));
            var convertedValueExpr = Expression.Convert(valueParam, typeof(T));
            Expression returnExpression = Expression.Assign(Expression.Field
            (Expression.Convert(sourceParam, source), fieldInfo), convertedValueExpr);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(T));
            }
            var lambda = Expression.Lambda(typeof(Action<object, T>),
                returnExpression, sourceParam, valueParam);
            return (Action<object, T>)lambda.Compile();
        }
        return null;
    }

    public static Action<object, object> CreateLambdaFieldSetter(Type source, string fieldName)
    {
        var fieldInfo = GetFieldInfo(source, fieldName);
        if (fieldInfo != null)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            var valueParam = Expression.Parameter(typeof(object));
            var convertedValueExpr = Expression.Convert(valueParam, fieldInfo.FieldType);
            Expression returnExpression = Expression.Assign(Expression.Field
            (Expression.Convert(sourceParam, source), fieldInfo), convertedValueExpr);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            }
            var lambda = Expression.Lambda(typeof(Action<object, object>),
                returnExpression, sourceParam, valueParam);
            return (Action<object, object>)lambda.Compile();
        }
        return null;
    }

    /// <summary>
    /// Create a lambda function for an object instance target with a generic "T" argument
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    public static Func<object, T> CreateLambdaFieldGetter<T>(this Type source, string fieldName)
    {
        var fieldInfo = GetFieldInfo(source, fieldName);
        if (fieldInfo != null)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            Expression returnExpression = Expression.Field
            (Expression.Convert(sourceParam, source), fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(T));
            }
            var lambda = Expression.Lambda(returnExpression, sourceParam);
            return (Func<object, T>)lambda.Compile();
        }
        return null;
    }

    public static Func<object, object> CreateLambdaFieldGetter(this Type source, string fieldName)
    {
        var fieldInfo = GetFieldInfo(source, fieldName);
        if (fieldInfo != null)
        {
            var sourceParam = Expression.Parameter(typeof(object));
            Expression returnExpression = Expression.Field
            (Expression.Convert(sourceParam, source), fieldInfo);
            if (!fieldInfo.FieldType.IsClass)
            {
                returnExpression = Expression.Convert(returnExpression, typeof(object));
            }
            var lambda = Expression.Lambda(returnExpression, sourceParam);
            return (Func<object, object>)lambda.Compile();
        }
        return null;
    }

    public static Func<S, T> CreateFieldGetter<S, T>(FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
        DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] { typeof(S) }, true);
        ILGenerator gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
        }
        gen.Emit(OpCodes.Ret);
        return (Func<S, T>)setterMethod.CreateDelegate(typeof(Func<S, T>));
    }

    public static Action<S, T> CreateFieldSetter<S, T>(FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(S), typeof(T) }, true);
        ILGenerator gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field);
        }
        gen.Emit(OpCodes.Ret);
        return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
    }

    /// <summary>
    /// Returns a Lambda function that takes object[0] as paramter and returns object[1]
    /// </summary>
    /// <param name="methodInfo"> The Method the delegate will reflects </param>
    /// <returns></returns>
    public static Func<object, object> GetLambdaMethodFunction(MethodInfo methodInfo)
    {
        var parameterObject = Expression.Parameter(typeof(object), "obj");
        var convertParameterObject = Expression.Convert(parameterObject, methodInfo.GetParameters().First().ParameterType);

        var returnObject = Expression.Parameter(typeof(object), "returnObject");
        var convertReturnObject = Expression.Convert(returnObject, methodInfo.GetParameters().First().ParameterType);


        var call = Expression.Call(methodInfo, convertParameterObject, convertReturnObject);
        var lambda = Expression.Lambda<Func<object, object>>(call, parameterObject, returnObject);

        return lambda.Compile();
    }

    /// <summary>
    /// Return a delegate of a dynamic method to get the value of a field
    /// </summary>
    /// <typeparam name="S"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Func<S, T> CreateDynamicMethodFieldGetter<S, T>(FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
        DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] { typeof(S) }, true);
        ILGenerator gen = getterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
        }
        gen.Emit(OpCodes.Ret);
        return (Func<S, T>)getterMethod.CreateDelegate(typeof(Func<S, T>));
    }

    /// <summary>
    /// Return a delegate of a dynamic method to set the value of a field
    /// </summary>
    /// <typeparam name="S"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Action<S, T> CreateDynamicMethodFieldSetter<S, T>(FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(S), typeof(T) }, true);
        ILGenerator gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field);
        }
        gen.Emit(OpCodes.Ret);
        return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
    }

    /// <summary>
    /// Return a delegate of a dynamic method to get value of a field for generic classes.
    /// Slower than the non generic version
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Func<object, object> CreateDynamicMethodGenericFieldGetter(this FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
        DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(object), new[] { typeof(object) }, true);
        ILGenerator gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldsfld, field);
            gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, field.DeclaringType);
            gen.Emit(OpCodes.Ldfld, field);
            gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Box, field.FieldType);
        }
        gen.Emit(OpCodes.Ret);
        return (Func<object, object>)setterMethod.CreateDelegate(typeof(Func<object, object>));
    }

    /// <summary>
    /// Return a delegate of a dynamic method to set value of a field for generic classes.
    /// Slower than the non generic version
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Action<object, object> CreateDynamicMethodGenericFieldSetter(this FieldInfo field)
    {
        string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
        DynamicMethod setterMethod = new DynamicMethod(methodName, null, new[] { typeof(object), typeof(object) }, true);
        ILGenerator gen = setterMethod.GetILGenerator();
        if (field.IsStatic)
        {
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
            gen.Emit(OpCodes.Stsfld, field);
        }
        else
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Castclass, field.DeclaringType);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(field.FieldType.IsClass ? OpCodes.Castclass : OpCodes.Unbox_Any, field.FieldType);
            gen.Emit(OpCodes.Stfld, field);
        }
        gen.Emit(OpCodes.Ret);
        return (Action<object, object>)setterMethod.CreateDelegate(typeof(Action<object, object>));
    }

    /// <summary>
    /// Return a delegate of property getter method
    /// </summary>
    /// <typeparam name="S"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Func<S, T> CreatePropertyGetterDelegate<S, T>(PropertyInfo propertyInfo)
    {
        var getMethodInfo = propertyInfo.GetGetMethod();

        return (Func<S, T>)Delegate.CreateDelegate(typeof(Func<S, T>), null, getMethodInfo);
    }

    public static Func<T> CreateInstancePropertyGetterDelegate<T>(object instance, PropertyInfo propertyInfo)
    {
        var getMethodInfo = propertyInfo.GetGetMethod();

        return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), instance, getMethodInfo.Name, true);
    }

    /// <summary>
    /// Return a delegate of property setter method
    /// </summary>
    /// <typeparam name="S"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Action<S, T> CreatePropertySetterDelegate<S, T>(PropertyInfo propertyInfo)
    {
        var getMethodInfo = propertyInfo.GetSetMethod();

        return (Action<S, T>)Delegate.CreateDelegate(typeof(Action<S, T>), null, getMethodInfo);
    }

    public static Action<T> CreateInstancePropertySetterDelegate<T>(object instance, PropertyInfo propertyInfo)
    {
        var getMethodInfo = propertyInfo.GetSetMethod();

        return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), instance, getMethodInfo.Name, true);
    }

    /// <summary>
    /// Return a delegate lambda expression to get the value of a property
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <returns></returns>
    public static Func<object, object> GetLambdaPropertyGetter(PropertyInfo propertyInfo)
    {
        // Define our instance parameter, which will be the input of the Func
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        // 1. Cast the instance to the correct type
        var instanceExpr = Expression.TypeAs(objParameterExpr, propertyInfo.DeclaringType);
        // 2. Call the getter and retrieve the value of the property
        var propertyExpr = Expression.Property(instanceExpr, propertyInfo);
        // 3. Convert the property's value to object
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
        // Create a lambda expression of the latest call & compile it
        return Expression.Lambda<Func<object, object>>(propertyObjExpr, objParameterExpr).Compile();
    }

    public static Func<object, TValue> GetLambdaPropertyGetter<TValue>(PropertyInfo propertyInfo)
    {
        // Ensure the property belongs to the specified instance type
       /* if (propertyInfo.DeclaringType != typeof(TInstance))
        {
            throw new ArgumentException($"PropertyInfo does not belong to the specified type '{typeof(TInstance)}'.");
        }*/

        // Define strongly typed parameters
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        var instanceExpr = Expression.TypeAs(objParameterExpr, propertyInfo.DeclaringType);

        // Create the property getter expression
        var propertyExpr = Expression.Property(instanceExpr, propertyInfo);

        // Compile the lambda expression
        return Expression.Lambda<Func<object, TValue>>(propertyExpr, objParameterExpr).Compile();
    }

    /// <summary>
    /// Return a delegate lambda expression to set the value of a property
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <returns></returns>
    public static Action<object, object> GetLambdaPropertySetter(PropertyInfo propertyInfo)
    {
        // Define our instance parameter, which will be the input of the Func
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        // 1. Cast the instance to the correct type
        var instanceExpr = Expression.TypeAs(objParameterExpr, propertyInfo.DeclaringType);

        var setObject = Expression.Parameter(typeof(object), "setVal");
        var convertReturnObject = Expression.Convert(setObject, propertyInfo.GetSetMethod().GetParameters().First().ParameterType);
        var call = Expression.Call(instanceExpr, propertyInfo.GetSetMethod(), convertReturnObject);

        // Create a lambda expression of the latest call & compile it
        return Expression.Lambda<Action<object, object>>(call, objParameterExpr, setObject).Compile();
    }

    public static Action<object, TValue> GetLambdaPropertySetter<TValue>(PropertyInfo propertyInfo)
    {        
        // Define strongly typed parameters
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        var instanceExpr = Expression.TypeAs(objParameterExpr, propertyInfo.DeclaringType);

        var valueExpr = Expression.Parameter(typeof(TValue), "value");

        // Create the property setter call
        var call = Expression.Call(instanceExpr, propertyInfo.GetSetMethod(), valueExpr);

        // Compile the lambda expression
        return Expression.Lambda<Action<object, TValue>>(call, objParameterExpr, valueExpr).Compile();
    }

    public static Action<TInstance, TValue> GetLambdaPropertySetter<TInstance, TValue>(PropertyInfo propertyInfo)
    where TInstance : class
    {
        // Ensure the property belongs to the specified instance type
        if (propertyInfo.DeclaringType != typeof(TInstance))
        {
            throw new ArgumentException($"PropertyInfo does not belong to the specified type '{typeof(TInstance)}'.");
        }

        // Define strongly typed parameters
        var instanceExpr = Expression.Parameter(typeof(TInstance), "instance");
        var valueExpr = Expression.Parameter(typeof(TValue), "value");

        // Create the property setter call
        var call = Expression.Call(instanceExpr, propertyInfo.GetSetMethod(), valueExpr);

        // Compile the lambda expression
        return Expression.Lambda<Action<TInstance, TValue>>(call, instanceExpr, valueExpr).Compile();
    }
}
