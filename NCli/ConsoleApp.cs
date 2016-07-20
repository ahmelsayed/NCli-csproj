using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NCli
{
    public class ConsoleApp
    {
        public static Task RunAsync<T>(string[] args, IDependencyResolver dependencyResolver = null)
        {
            var app = new ConsoleApp(args, typeof(T).Assembly, dependencyResolver);
            var verb = app.Parse();
            return verb.Run();
        }

        public static void Run<T>(string[] args, IDependencyResolver dependencyResolver = null)
        {
            Task.Run(() => RunAsync<T>(args, dependencyResolver)).Wait();
        }

        private readonly IDependencyResolver _dependencyResolver;
        private readonly string[] _args;
        private readonly IEnumerable<TypePair<VerbAttribute>> _verbs;
        private readonly string _cliName;

        internal ConsoleApp(string[] args, Assembly assembly, IDependencyResolver dependencyResolver)
        {
            _args = args?.Length < 1 ? new string[1] { "help" } : args;
            _dependencyResolver = dependencyResolver;
            _cliName = Process.GetCurrentProcess().ProcessName;
            _verbs = assembly
                .GetTypes()
                .Where(t => typeof(IVerb).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => new TypePair<VerbAttribute> { Type = t, Attribute = TypeToAttribute(t) });
        }

        internal IVerb Parse()
        {
            var verbType = GetVerbType(_args[0]);
            var verb = InstantiateType<IVerb>(verbType?.Type);
            verb.OriginalVerb = _args[0];
            verb.DependencyResolver = _dependencyResolver;
            if (_args.Length == 1)
            {
                return verb;
            }

            var stack = new Stack<string>(_args.Skip(1).Reverse());

            foreach (var option in verbType.Options)
            {
                if (option.Attribute.DefaultValue != null)
                {
                    option.PropertyInfo.SetValue(verb, option.Attribute.DefaultValue);
                }
            }

            var orderedOptions = new Stack<PropertyInfo>(verbType.Options.Where(o => o.Attribute._order != -1).OrderBy(o => o.Attribute._order).Select(o => o.PropertyInfo).Reverse().ToArray());
            object value;
            while (stack.Any() && orderedOptions.Any())
            {
                var orderedOption = orderedOptions.Pop();
                if (TryParseOption(orderedOption, stack, out value))
                {
                    orderedOption.SetValue(verb, value);
                }
            }

            while (stack.Any())
            {
                if (!stack.Any()) break;
                var arg = stack.Pop();
                PropertyInfo option = null;
                if (arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
                {
                    option = verbType.Options.SingleOrDefault(o => o.Attribute._longName.Equals(arg.Substring(2), StringComparison.OrdinalIgnoreCase))?.PropertyInfo;
                }
                else if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase) && arg.Length == 2)
                {
                    option = verbType.Options.SingleOrDefault(o => o.Attribute._shortName.ToString().Equals(arg.Substring(1), StringComparison.OrdinalIgnoreCase))?.PropertyInfo;
                }

                if (option == null)
                {
                    throw new ParseException($"Unable to find option {arg} on {_args[0]}");
                }

                if (TryParseOption(option, stack, out value))
                {
                    option.SetValue(verb, value);
                }
            }
            return verb;
        }

        private TypePair<VerbAttribute> GetVerbType(string verbName)
        {
            return _verbs.SingleOrDefault(p => p.Attribute.Names.Any(n => n.Equals(verbName, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool TryParseOption(PropertyInfo option, Stack<string> args, out object value)
        {
            value = null;
            if (option.PropertyType.IsGenericEnumerable())
            {
                var genericType = option.PropertyType.GetEnumerableType();
                var values = genericType.CreateList();
                while (args.Any() && !args.Peek().StartsWith("-"))
                {
                    var arg = args.Pop();
                    object temp;
                    if (TryCast(arg, genericType, out temp))
                    {
                        values.Add(temp);
                    }
                    else
                    {
                        args.Push(arg);
                        break;
                    }
                }
                value = values;
                return values.Count != 0;
            }
            else if (option.PropertyType == typeof(bool))
            {
                value = true;
                return true;
            }
            else
            {
                var arg = args.Pop();
                object temp;
                if (!arg.StartsWith("-") && TryCast(arg, option.PropertyType, out temp))
                {
                    value = temp;
                    return true;
                }
                else
                {
                    args.Push(arg);
                    return false;
                }
            }
        }

        private static bool TryCast(string arg, Type type, out object obj)
        {
            obj = null;
            try
            {
                if (type.GetTypeInfo().IsEnum)
                {
                    obj = Enum.Parse(type, arg, ignoreCase: true);
                }
                else if (type == typeof(string))
                {
                    obj = arg;
                }
                else if (type == typeof(DateTime))
                {
                    obj = DateTime.Parse(arg);
                }
                else if (type == typeof(int))
                {
                    obj = int.Parse(arg);
                }
                else if (type == typeof(long))
                {
                    obj = long.Parse(arg);
                }
                return obj != null;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unable to parse ({arg}) as {type.Name}");
                var s = e;
                return false;
            }
        }

        private static VerbAttribute TypeToAttribute(Type type)
        {
            var attribute = type.GetTypeInfo().GetCustomAttribute<VerbAttribute>();
            var verbIndex = type.Name.LastIndexOf("verb", StringComparison.OrdinalIgnoreCase);
            var verbName = verbIndex == -1 || verbIndex == 0 ? type.Name : type.Name.Substring(0, verbIndex);
            verbName = verbName.ToLowerInvariant();

            if (attribute == null)
            {
                return new VerbAttribute(verbName);
            }
            else if (attribute.Names == null || attribute.Names.Length == 0)
            {
                return new VerbAttribute(verbName)
                {
                    HelpText = attribute.HelpText,
                    ShowInHelp = attribute.ShowInHelp,
                    Usage = attribute.Usage
                };
            }
            else
            {
                return attribute;
            }
        }

        private T InstantiateType<T>(Type type)
        {
            var ctor = type.GetConstructors().SingleOrDefault();
            var args = ctor?.GetParameters().Select(p => ResolveType(p.ParameterType)).ToArray();
            if (args == null || args.Length == 0)
            {
                return (T)Activator.CreateInstance(type);
            }
            else
            {
                return (T)Activator.CreateInstance(type, args);
            }
        }

        private object ResolveType(Type type)
        {
            if (type == typeof(HelpText))
            {
                return new HelpText(BuildHelp());
               
            }
            else
            {
                return _dependencyResolver?.GetService(type);
            }
        }

        private IEnumerable<HelpLine> BuildHelp()
        {
            if (_args.Length > 1)
            {
                var verb = GetVerbType(_args[1]);
                if (verb != null)
                {
                    yield return new HelpLine { Value = $"Usage: {_cliName} {verb.Attribute.Usage} [Options]", Level = TraceLevel.Info };
                    yield return new HelpLine { Value = "\t", Level = TraceLevel.Info };

                    var longestOption = verb.Options.Select(s => s.Attribute.GetUsage(s.PropertyInfo.Name)).Select(s => s.Length).Max();
                    foreach (var option in verb.Options)
                    {
                        yield return new HelpLine { Value = string.Format($"   {{0, {-longestOption}}} {{1}}", option.Attribute.GetUsage(option.PropertyInfo.Name), option.Attribute.HelpText), Level = TraceLevel.Info };
                    }
                    yield break;
                }
            }

            foreach (var help in GeneralHelp())
            {
                yield return help;
            }
        }

        private IEnumerable<HelpLine> GeneralHelp()
        {
            yield return new HelpLine { Value = $"Usage: {_cliName} [verb] [Options]", Level = TraceLevel.Info };
            yield return new HelpLine { Value = "\t", Level = TraceLevel.Info };

            var longestName = _verbs.Select(p => p.Attribute).Max(v => v.Names.Max(n => n.Length));
            foreach (var verb in _verbs)
            {
                foreach (var name in verb.Attribute.Names)
                {
                    yield return new HelpLine { Value = string.Format($"   {{0, {-longestName}}}  {{1}}", name, verb.Attribute.HelpText), Level = TraceLevel.Info };
                }
            }
        }
    }
}
