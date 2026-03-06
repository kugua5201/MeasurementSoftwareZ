using Castle.DynamicProxy;
using HandyControl.Controls;
using MeasurementSoftware.Services.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeasurementSoftware.Interceptors
{

    public class AcquiringInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            var type = invocation.InvocationTarget.GetType();
            var field = type.GetField("_recipeConfigService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?? type.BaseType?.GetField("_recipeConfigService", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            IRecipeConfigService recipeConfigService = null;
            if (field != null)
            {
                recipeConfigService = field.GetValue(invocation.InvocationTarget) as IRecipeConfigService;
            }

            if (recipeConfigService != null && recipeConfigService.IsCollecting)
            {
                Growl.Warning("当前正在采集中，无法进行操作");

                // 修复关键点：处理不同返回类型
                if (invocation.Method.ReturnType == typeof(Task))
                {
                    // 返回 Task 的方法不能返回 null，必须返回 Task.CompletedTask
                    invocation.ReturnValue = Task.CompletedTask;
                }
                else if (invocation.Method.ReturnType.IsGenericType && invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // 如果是 Task<T>，返回含默认值的完成 Task
                    var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
                    var defaultResult = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                    var taskFromResultMethod = typeof(Task).GetMethod("FromResult").MakeGenericMethod(resultType);
                    invocation.ReturnValue = taskFromResultMethod.Invoke(null, new[] { defaultResult });
                }
                // 拦截执行
                return; 
            }

            invocation.Proceed();
        }
    }
}
