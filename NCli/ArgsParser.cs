using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NCli
{
    public static class ArgsParser
    {
        public static IDependencyResolver DependencyResolver { get; set; }

        public static IVerb Parse(string[] args)
        {
            return Parse(args, Assembly.GetEntryAssembly());
        }

        public static IVerb Parse(string[] args, Assembly assembly)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }
            else if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var verbTypes = assembly.GetTypes().Where(t => typeof(IVerb).IsAssignableFrom(t));
            var verbs = verbTypes.Zip(verbTypes.Select(TypeToAttribute), (t, a) => new { type = t, attribute = a });

            if (args.Length == 0)
            {
                args = new[] { "help" };
            }

            var verbType = verbs.Single(v => v.attribute.Names.Any(n => n.Equals(args[0], StringComparison.OrdinalIgnoreCase)));
            var verb = InstantiateType<IVerb>(verbType.type);
            verb.OriginalVerb = args[0];
            if (args.Length == 1)
            {
                return verb;
            }

            var stack = new Stack<string>(args.Skip(1).Reverse());

            var options = verbType.type.
                GetProperties()
                .Select(p => new { property = p, attribute = p.GetCustomAttribute<OptionAttribute>() })
                .Where(a => a.attribute != null);

            foreach (var option in options)
            {
                if (option.attribute.DefaultValue != null)
                {
                    option.property.SetValue(verb, option.attribute.DefaultValue);
                }
            }

            var orderedOptions = new Stack<PropertyInfo>(options.Where(o => o.attribute._order != -1).OrderBy(o => o.attribute._order).Select(o => o.property).Reverse().ToArray());
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
                    option = options.SingleOrDefault(o => o.attribute._longName.Equals(arg.Substring(2), StringComparison.OrdinalIgnoreCase))?.property;
                }
                else if (arg.StartsWith("-", StringComparison.OrdinalIgnoreCase) && arg.Length == 2)
                {
                    option = options.SingleOrDefault(o => o.attribute._shortName.ToString().Equals(arg.Substring(1), StringComparison.OrdinalIgnoreCase))?.property;
                }

                if (option == null)
                {
                    throw new ParseException($"Unable to find option {arg} on {args[0]}");
                }

                if (TryParseOption(option, stack, out value))
                {
                    option.SetValue(verb, value);
                }
            }
            return verb;
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
                if (!arg.StartsWith("-")  && TryCast(arg, option.PropertyType, out temp))
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
            var verbName = verbIndex == -1 ? type.Name : type.Name.Substring(0, verbIndex);

            if (attribute == null)
            {
                return new VerbAttribute(verbName);
            }
            else if (attribute.Names == null || attribute.Names.Length == 0)
            {
                return new VerbAttribute(verbName)
                {
                    HelpText = attribute.HelpText,
                    Show = attribute.Show,
                    Usage = attribute.Usage
                };
            }
            else
            {
                return attribute;
            }
        }

        private static T InstantiateType<T>(Type type)
        {
            var ctor = type.GetConstructors().SingleOrDefault();
            var args = ctor?.GetParameters().Select(p => DependencyResolver.GetService(p.ParameterType)).ToArray();
            if (args == null || args.Length == 0)
            {
                return (T)Activator.CreateInstance(type);
            }
            else
            {
                return (T)Activator.CreateInstance(type, args);
            }
        }
    }
}
