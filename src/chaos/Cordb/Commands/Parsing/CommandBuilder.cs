using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace chaos.Cordb.Commands
{
    class CommandBuilder
    {
        private IServiceProvider serviceProvider;

        public CommandBuilder(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
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

                var instance = new Lazy<object>(() =>
                {
                    var ctor = type.GetConstructors().Single();
                    var ctorParameters = ctor.GetParameters().Select(p => serviceProvider.GetService(p.ParameterType)).ToArray();
                    return Activator.CreateInstance(type, ctorParameters);
                });

                if (typeof(ICustomCommandParser).IsAssignableFrom(type))
                {
                    customCommands.Add(ParseCustomCommand(type, instance, rootCommand));
                    continue;
                }

                var methods = type.GetMethods();

                foreach (var method in methods)
                {
                    ProcessMethod(method, type, instance, rootCommand);
                }
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

                        if (ex is InvalidCommandException)
                            context.Console.Error.WriteLine(ex.Message);
                        else
                            context.Console.Error.WriteLine(ex.ToString());

                        Console.ResetColor();
                    }
                });

            var parser = commandLineBuilder.Build();

            return new RelayParser(parser, customCommands.ToArray());
        }

        private string ParseCustomCommand(Type type, Lazy<object> instance, RootCommand rootCommand)
        {
            var commandAttrib = type.GetCustomAttribute<CommandAttribute>();

            if (commandAttrib == null)
                throw new ArgumentNullException();

            var cliCommand = new Command(commandAttrib.Name);

            var remainder = new Argument<string>(() => string.Empty);

            cliCommand.AddArgument(remainder);

            cliCommand.SetHandler(ctx =>
            {
                try
                {
                    var args = ctx.ParseResult.GetValueForArgument(remainder);

                    var argParser = new ArgParser(args);

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

                var argParser = new ArgParser(args);

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

        private void ProcessMethod(MethodInfo method, Type type, Lazy<object> instance, RootCommand rootCommand)
        {
            var commandAttrib = method.GetCustomAttribute<CommandAttribute>();

            if (commandAttrib != null)
            {
                var cliCommand = new Command(commandAttrib.Name);

                var parameters = method.GetParameters();

                foreach (var parameter in parameters)
                {
                    var parameterAttrib = parameter.GetCustomAttribute<OptionAttribute>();

                    if (parameterAttrib == null)
                        throw new NotImplementedException($"Parameter {type.Name}.{method.Name} -> {parameter.Name} was missing a {nameof(OptionAttribute)}");

                    var cliOptType = typeof(Option<>).MakeGenericType(parameter.ParameterType);

                    var cliOpt = (Option) Activator.CreateInstance(
                        cliOptType,
                        new object[] {
                            parameterAttrib.Name, //name
                            null //description
                        }
                    );

                    cliCommand.AddOption(cliOpt);
                }

                cliCommand.SetHandler(ctx =>
                {
                    var vals = cliCommand.Options.Select(v => ctx.ParseResult.GetValueForOption(v)).ToArray();

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
            }
        }
    }
}
