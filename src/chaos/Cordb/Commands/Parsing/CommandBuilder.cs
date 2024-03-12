using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ChaosDbg;
using ChaosDbg.Commands;
using IConsole = ChaosDbg.IConsole;

namespace chaos.Cordb.Commands
{
    class CommandBuilder
    {
        private IServiceProvider serviceProvider;

        //If one command takes another command through dependency injection, we need to be able to resolve these
        private Dictionary<Type, Lazy<object>> commandToInstanceMap = new Dictionary<Type, Lazy<object>>();
        private Stack<Type> resolveCommandStack = new Stack<Type>();
        private IConsole console;

        public CommandBuilder(IServiceProvider serviceProvider, IConsole console)
        {
            this.serviceProvider = serviceProvider;
            this.console = console;
        }

        public RelayParser Build()
        {
            var types = typeof(Program).Assembly.GetTypes();

            var rootCommand = new RootCommand();

            var customCommands = new List<string>();

            foreach (var type in types)
            {
                if (type.IsInterface)
                    continue;

                //We don't know if we'll end up needing this instance
                var instance = new Lazy<object>(() =>
                {
                    var ctor = type.GetConstructors().Single();
                    var ctorParameters = ctor.GetParameters().Select(p =>
                    {
                        //Command A uses Command B. Command A was added to the stack.
                        //If Command B also uses Command A, there's a recursive reference
                        if (resolveCommandStack.Contains(p.ParameterType))
                        {
                            var str = string.Join(" -> ", resolveCommandStack.Reverse().Select(r => r.Name));

                            throw new InvalidOperationException($"Cannot resolve service '{p.ParameterType.Name}': a recursive reference was found in hierarchy {str} -> {type.Name} -> {p.ParameterType.Name}.");
                        }

                        if (commandToInstanceMap.TryGetValue(p.ParameterType, out var commandInstance))
                            return ResolveCommandInstance(type, commandInstance);

                        return serviceProvider.GetService(p.ParameterType);
                    }).ToArray();
                    return Activator.CreateInstance(type, ctorParameters);
                });

                if (typeof(ICustomCommandParser).IsAssignableFrom(type))
                {
                    commandToInstanceMap[type] = instance;
                    customCommands.Add(ParseCustomCommand(type, instance, rootCommand));
                    continue;
                }

                var methods = type.GetMethods();

                var hadCommands = false;

                foreach (var method in methods)
                {
                    hadCommands |= ProcessMethod(method, type, instance, rootCommand);
                }

                if (hadCommands)
                    commandToInstanceMap[type] = instance;
            }

            var commandLineBuilder = new CommandLineBuilder(rootCommand);

            commandLineBuilder
                .AddMiddleware(async (context, next) =>
                {
                    try
                    {
                        await next(context);
                    }
                    catch (Exception ex)
                    {
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.Red;

                        if (ex is InvalidCommandException or InvalidExpressionException)
                            context.Console.Error.WriteLine(ex.Message);
                        else
                            context.Console.Error.WriteLine(ex.ToString());

                        Console.ResetColor();
                    }
                });

            var parser = commandLineBuilder.Build();

            return new RelayParser(parser, customCommands.ToArray());
        }

        private object ResolveCommandInstance(Type parentType, Lazy<object> instance)
        {
            //Command A takes Command B as a parameter. It's possible that there could be
            //a recursive reference between A -> B -> A, so we need to protect against that

            resolveCommandStack.Push(parentType);

            try
            {
                return instance.Value;
            }
            finally
            {
                resolveCommandStack.Pop();
            }
        }

        private string ParseCustomCommand(Type type, Lazy<object> instance, RootCommand rootCommand)
        {
            var commandAttrib = type.GetCustomAttribute<CommandAttribute>();

            if (commandAttrib == null)
                throw new ArgumentNullException();

            var cliCommand = new Command(commandAttrib.Name);

            var remainder = new Argument<string[]>(Array.Empty<string>)
            {
                Arity = new ArgumentArity(0, 100000) //Maximum arity. Ensures that we can type ? 1 + 1 with spaces between the args and everything we type will get passed to the ? command
            };

            cliCommand.AddArgument(remainder);

            cliCommand.SetHandler(ctx =>
            {
                try
                {
                    var args = ctx.ParseResult.GetValueForArgument(remainder);

                    var argParser = new ArgParser(string.Join(" ", args));

                    ((ICustomCommandParser) instance.Value).Parse(argParser).Invoke();
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            });

            cliCommand.AddValidator(result =>
            {
                var args = result.GetValueForArgument(remainder);

                var argParser = new ArgParser(string.Join(" ", args));

                var validationResult = ((ICustomCommandParser) instance.Value).Parse(argParser);

                if (argParser.ErrorMessage != null)
                    result.ErrorMessage = argParser.ErrorMessage;
                else if (!argParser.End)
                    result.ErrorMessage = $"Extra characters '{argParser.Remaining}' in command";
                else if (validationResult == null)
                    result.ErrorMessage = "Invalid command";
            });

            rootCommand.AddCommand(cliCommand);

            return commandAttrib.Name;
        }

        private bool ProcessMethod(MethodInfo method, Type type, Lazy<object> instance, RootCommand rootCommand)
        {
            var commandAttrib = method.GetCustomAttribute<CommandAttribute>();

            if (commandAttrib == null)
                return false;

            var cliCommand = new Command(commandAttrib.Name);

            var parameters = method.GetParameters();

            foreach (var parameter in parameters)
            {
                ICommandParser commandParser = null;
                var commandParserAttrib = parameter.ParameterType.GetCustomAttribute<CommandParserAttribute>();

                if (commandParserAttrib != null)
                {
                    var parserType = commandParserAttrib.Type;

                    if (parserType.IsGenericType)
                        parserType = parserType.MakeGenericType(parameter.ParameterType);

                    var instanceFieldInfo = parserType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);

                    if (instanceFieldInfo == null)
                        throw new MissingMemberException(parserType.Name, "Instance");

                    commandParser = (ICommandParser) instanceFieldInfo.GetValue(null);
                }

                var optionAttrib = parameter.GetCustomAttribute<OptionAttribute>();

                if (optionAttrib != null)
                {
                    var cliOptType = typeof(Option<>).MakeGenericType(parameter.ParameterType);

                    var cliOpt = (Option) Activator.CreateInstance(
                        cliOptType,
                        new object[] {
                            optionAttrib.Name, //name
                            null               //description
                        }
                    );

                    cliCommand.AddOption(cliOpt);
                }
                else
                {
                    var argAttrib = parameter.GetCustomAttribute<ArgumentAttribute>();

                    if (argAttrib == null)
                        throw new NotImplementedException($"Parameter {type.Name}.{method.Name} -> {parameter.Name} must have either an {nameof(ArgumentAttribute)} or {nameof(OptionAttribute)}");

                    var cliArgType = typeof(Argument<>).MakeGenericType(parameter.ParameterType);

                    Argument cliArg;

                    /* Possible ctors:
                     * - string name, string description
                     * - string name, Func<T> getDefaultValue, string description
                     * - Func<T> getDefaultValue
                     * - string name, ParseArgument<T> parse, bool isDefault, string description
                     * - ParseArgument<T> parse, bool isDefault */

                    if (commandParser != null)
                    {
                        //We need to create a closure so we can have our ICommandParser create the result.
                        //We do this by creating a func with a closure that returns an object, and then passing that
                        //into a method we can create a generic instance out of in order to wrap it in another delegate
                        //that returns a value of type T
                        Func<ArgumentResult, object> inner = r =>
                        {
                            var raw = r.Tokens.Single().Value;
                            var result = commandParser.Parse(raw);

                            if (result == null)
                                throw new InvalidOperationException($"Could not convert '{raw}' to a value of type '{parameter.ParameterType}'");

                            return result;
                        };

                        var outer = GetType()
                            .GetMethod(nameof(ConvertParseArgument), BindingFlags.Static | BindingFlags.NonPublic)
                            .MakeGenericMethod(parameter.ParameterType)
                            .Invoke(null, new object[] {inner});

                        cliArg = (Argument) Activator.CreateInstance(
                            cliArgType,
                            new object[] {
                                parameter.Name,
                                outer,
                                parameter.HasDefaultValue,
                                null
                            }
                        );
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        var getDefaultValue = GetType()
                            .GetMethod(nameof(GetDefaultValue), BindingFlags.Static | BindingFlags.NonPublic)
                            .MakeGenericMethod(parameter.ParameterType)
                            .CreateDelegate(typeof(Func<>).MakeGenericType(parameter.ParameterType));

                        cliArg = (Argument) Activator.CreateInstance(
                            cliArgType,
                            new object[] {
                                parameter.Name,  //name
                                getDefaultValue, //getDefaultValue
                                null             //description
                            }
                        );
                    }
                    else
                    {
                        cliArg = (Argument) Activator.CreateInstance(
                            cliArgType,
                            new object[] {
                                parameter.Name,  //name
                                null             //description
                            }
                        );
                    }

                    cliCommand.AddArgument(cliArg);
                }
            }

            cliCommand.SetHandler(ctx =>
            {
                var vals = 
                    cliCommand.Arguments.Select(v => ctx.ParseResult.GetValueForArgument(v)).Concat(
                        cliCommand.Options.Select(v => ctx.ParseResult.GetValueForOption(v))
                    ).ToArray();

                try
                {
                    method.Invoke(instance.Value, vals);
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            });

            rootCommand.AddCommand(cliCommand);

            return true;
        }

        private static T GetDefaultValue<T>() => default;

        private static ParseArgument<T> ConvertParseArgument<T>(Func<ArgumentResult, object> func)
        {
            return r => (T) func(r);
        }
    }
}
