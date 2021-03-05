﻿namespace wave.syntax
{
    using System.Collections.Generic;

    public class ParameterSyntax : BaseSyntax
    {
        public ParameterSyntax(string type, string identifier)
            : this(new TypeSyntax(type), identifier)
        {
        }

        public ParameterSyntax(TypeSyntax type, string identifier)
        {
            Type = type;
            Identifier = identifier;
        }

        public override SyntaxType Kind => SyntaxType.Parameter;

        public override IEnumerable<BaseSyntax> ChildNodes => GetNodes(Type);

        public List<ModificatorSyntax> Modifiers { get; set; } = new();

        public TypeSyntax Type { get; set; }

        public string Identifier { get; set; }
    }
}