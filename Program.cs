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
        private static (IEnumerable<MethodInfo> entryPoints, IEnumerable<MethodInfo> mods) TryLoad(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (File.Exists(path) && (ext == ".dll" || ext == ".exe"))
            {
                var assem = Assembly.LoadFrom(path);
                if (assem == Assembly.GetExecutingAssembly()) return (Array.Empty<MethodInfo>(), Array.Empty<MethodInfo>());

                if (assem.EntryPoint != null) return (new[] { assem.EntryPoint }, Array.Empty<MethodInfo>());
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

                    return (Array.Empty<MethodInfo>(), 
                        types.Where(t => t.GetCustomAttribute<PluginAttribute>() != null)
                             .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                             .Where(m => m.GetCustomAttribute<PluginAttribute>() != null));
                }
            }
            else if (Directory.Exists(path))
            {
                return Directory.EnumerateFileSystemEntries(path).Select(TryLoad)
                    .Aggregate((a, b) => (a.entryPoints.Concat(b.entryPoints), a.mods.Concat(b.mods)));
            }
            else
                return (Array.Empty<MethodInfo>(), Array.Empty<MethodInfo>());
        }

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
                    var (entry, mods) = TryLoad(assems);
                    entries.AddRange(entry);
                    preEntry.AddRange(mods);
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
