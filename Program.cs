using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LoadBins
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class PluginAttribute : Attribute { }

    class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            var entries = new List<MethodInfo>();
            var preEntry = new List<MethodInfo>();
            var entryArgs = new List<string>();
            bool done = false;
            foreach (var assems in args)
            {
                if (assems == "--") 
                { 
                    done = true;
                    continue;
                }

                if (!done)
                {
                    if (File.Exists(assems))
                    {
                        var assem = Assembly.LoadFrom(assems);

                        if (assem.EntryPoint != null) entries.Add(assem.EntryPoint);
                        else
                        {
                            Type[] types;
                            try
                            {
                                types = assem.GetTypes();
                            }
                            catch (ReflectionTypeLoadException e)
                            {
                                types = e.Types;
                            }

                            foreach (var type in types
                                .Where(t => t.GetCustomAttribute<PluginAttribute>() != null))
                            {
                                preEntry.AddRange(type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    .Where(m => m.GetCustomAttribute<PluginAttribute>() != null));
                            }
                        }
                    }
                }
                else
                {
                    entryArgs.Add(assems);
                }
            }

            foreach (var pre in preEntry)
            {
                var param = new List<object>();

                foreach (var p in pre.GetParameters())
                {
                    if (p.ParameterType == typeof(List<MethodInfo>))
                        param.Add(entries);
                    else if (p.ParameterType == typeof(List<string>))
                        param.Add(entryArgs);
                    else if (p.ParameterType.IsValueType)
                        param.Add(Activator.CreateInstance(p.ParameterType));
                    else
                        param.Add(null);
                }

                pre.Invoke(null, param.ToArray());
            }

            var finalArgs = entryArgs.ToArray();

            foreach (var m in entries)
            {
                var param = new List<object>();

                foreach (var p in m.GetParameters())
                {
                    if (p.ParameterType == typeof(string[]))
                        param.Add(finalArgs);
                    else if (p.ParameterType.IsValueType)
                        param.Add(Activator.CreateInstance(p.ParameterType));
                    else
                        param.Add(null);
                }

                m.Invoke(null, param.ToArray());
            }
        }
    }
}
