using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReflectionPerformanceLab
{
    class Program
    {
        //运行一百万次
        public const int times = 1000000;

        //直接调用
        public static void DirectUse(AnObject o, object parameter)
        {
            var sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < times; i++)
            {
                o.Call(parameter);
            }
            sw.Stop();
            Console.WriteLine("直接调用方法: " + sw.ElapsedMilliseconds);

            sw.Restart();
            for (int i = 0; i < times; i++)
            {
                var a = o.a;
            }
            sw.Stop();
            Console.WriteLine("直接获取属性: " + sw.ElapsedMilliseconds);
        }

        //反射
        public static void ReflectionUse(AnObject o, object parameter)
        {
            var parameters = new[] { parameter };

            var sw = new Stopwatch();
            sw.Start();

            var methodInfo = typeof(AnObject).GetMethod("Call");
            for (int i = 0; i < times; i++)
            {
                methodInfo.Invoke(o, parameters);
            }
            sw.Stop();
            Console.WriteLine("反射调用方法: " + sw.ElapsedMilliseconds);

            sw.Restart();

            var properties = typeof(AnObject).GetProperty("a");

            for (int i = 0; i < times; i++)
            {
                var a = properties.GetValue(o, null);
            }
            sw.Stop();
            Console.WriteLine("反射获取属性: " + sw.ElapsedMilliseconds);
        }

        public delegate void CallBack(object o1);

        public delegate int ReadProperty();

        //委托调用
        public static void DelegateUse(AnObject o, object parameter)
        {
            var parameters = new[] { parameter };

            var sw = new Stopwatch();
            sw.Start();

            var methodInfo = typeof(AnObject).GetMethod("Call");

            //通过类型对象和方法名建立强类型委托
            var del = (CallBack)Delegate.CreateDelegate(typeof(CallBack), o, methodInfo);

            for (int i = 0; i < times; i++)
            {
                //只能传入和输入方法相同类型和个数的输入，否则会导致无法通过编译
                del.Invoke(o);
            }
            sw.Stop();
            Console.WriteLine("委托调用方法: " + sw.ElapsedMilliseconds);

            sw.Restart();

            //获得a的getter的方法信息，getter的方法签名为无输入参数，有一个int的输出
            methodInfo = typeof(AnObject).GetMethod("get_a");

            var del2 = (ReadProperty)Delegate.CreateDelegate(typeof(ReadProperty), o, methodInfo);

            //使用委托来调用getter获取属性
            for (int i = 0; i < times; i++)
            {
                var a = del2();
            }
            sw.Stop();
            Console.WriteLine("委托获取属性: " + sw.ElapsedMilliseconds);
        }
        
        //getter辅助类
        //使用泛型
        public class GetterWrapper<TTarget>
        {
            private readonly Func<TTarget> _getter;

            //构造函数传入欲获得字段的propertyInfo
            public GetterWrapper(AnObject o, PropertyInfo propertyInfo)
            {
                var m = propertyInfo.GetGetMethod(true);

                //构造一个强类型的委托
                _getter = (Func<TTarget>)Delegate.CreateDelegate(typeof(Func<TTarget>), o, m);
            }

            public TTarget GetValue()
            {
                return _getter();
            }
        }

        public static void GenericDelegateUse()
        {
            var sw = new Stopwatch();
            sw.Start();

            var o = new AnObject(999);
            var propertyInfo = typeof(AnObject).GetProperty("a");

            //试图获得int类型的值
            var genericDel = new GetterWrapper<int>(o, propertyInfo);
            object a = 0;

            for (var i = 0; i < times; i++)
            {
                a = genericDel.GetValue();
            }
            sw.Stop();

            Console.WriteLine("属性的值：" + a); //999 
            Console.WriteLine("通用泛型委托获取属性: " + sw.ElapsedMilliseconds);

            sw.Restart();
            o.b = "test";
            propertyInfo = typeof(AnObject).GetProperty("b");

            //试图获得string类型的值
            var genericDel2 = new GetterWrapper<string>(o, propertyInfo);
            string b = "";

            for (var i = 0; i < times; i++)
            {
                b = genericDel2.GetValue();
            }
            sw.Stop();

            Console.WriteLine("属性的值：" + b); //test
            Console.WriteLine("通用泛型委托获取属性: " + sw.ElapsedMilliseconds);
        }

        //借助表达式
        public static Action<AnObject, object> GetAction()
        {
            //这个Action的输入为Program的一个实例和一个变量，没有输出
            Expression<Action<AnObject, object>> exp = (o, parameter) => o.Call(parameter);

            //调用Compile方法获得委托
            return exp.Compile();
        }
        public static Func<AnObject, int> GetFunc(object o)
        {
            //一个变量表达式，类型为object
            var target = Expression.Parameter(typeof(AnObject));

            //要获得的属性名
            var property = typeof(AnObject).GetProperty("a");

            //Property表达式获得属性的值
            var getPropertyValue = Expression.Property(target, property);

            //编译
            return Expression.Lambda<Func<AnObject, int>>(getPropertyValue, target).Compile();
        }

        //使用表达式
        public static void ExpressionUse(AnObject o, object parameter)
        {
            var sw = new Stopwatch();
            sw.Start();

            var exp = GetAction();

            for (var i = 0; i < times; i++)
            {
                exp(o, parameter);
            }
            sw.Stop();

            Console.WriteLine("表达式调用方法: " + sw.ElapsedMilliseconds);

            sw.Start();
            var exp2 = GetFunc(o);

            for (var i = 0; i < times; i++)
            {
                var a = exp2(o);
            }
            sw.Stop();

            Console.WriteLine("表达式获得属性: " + sw.ElapsedMilliseconds);
        }


        static void Main(string[] args)
        {
            var o = new AnObject(1);

            //强迫编译器JIT编译，生成方法的本地代码
            //如果没有这步，后面方法的直接调用将会加入JIT编译的时间（尽管只有一点点）
            //使得结果不准确
            o.Call(null);

            DirectUse(o, new object());
            ReflectionUse(o, new object());
            DelegateUse(o, new object());
            GenericDelegateUse();
            ExpressionUse(o, new object());
            Console.ReadKey();
        }
    }


    public class AnObject
    {
        public int a { get; set; }
        public string b { get; set; }
        public DateTime c { get; set; }

        public void Call(object o1)
        {

        }

        public AnObject(int a)
        {
            this.a = a;
        }
    }

}
