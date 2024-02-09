using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.Tests
{
    class IDAFunctionMetadata : IDAMetadata
    {
        public string Name { get; }

        public IDALine[] Lines { get; }

        public IDALine[] Code { get; }

        public List<string> AdditionalParents { get; } = new List<string>();

        public IDAFunctionMetadata(IDALine[] lines)
        {
            Lines = lines;
            Code = lines.Where(v => v.Kind == IDALineKind.Code).ToArray();

            if (Lines[0].Content.StartsWith(IDAComparer.IdaString_StartOfFunctionChunk))
            {
                Name = Lines[0].Content.Substring(IDAComparer.IdaString_StartOfFunctionChunk.Length);
            }
            else
            {
                var nameInfo = lines.First(l => l.Kind == IDALineKind.Info);

                var space = nameInfo.Content.IndexOf(' ');

                if (space == -1)
                    throw new NotImplementedException($"Don't know how to find function name from content '{nameInfo.Content}'. Couldn't find a space");

                Name = nameInfo.Content.Substring(0, space);

                if (Name == string.Empty)
                {
                    var @public = nameInfo.Content.IndexOf("public");

                    if (@public != -1)
                        Name = nameInfo.Content.Substring(@public + 7);
                    else
                        throw new NotImplementedException($"Don't know how to find function name from content '{nameInfo.Content}'. Couldn't find the word 'public'");
                }
            }

            foreach (var additionalParent in Lines.Skip(1).TakeWhile(v => v.Content.StartsWith(IDAComparer.IdaString_AdditionalParentFunction)))
            {
                var space = additionalParent.Content.LastIndexOf(' ');
                var additionalName = additionalParent.Content.Substring(space + 1);

                AdditionalParents.Add(additionalName);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
