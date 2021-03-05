﻿namespace wave.syntax
{
    using Sprache;

    public class SpaceSyntax : DirectiveSyntax, IPositionAware<SpaceSyntax>
    {
        public LiteralExpressionSyntax Value { get; set; }

        public override DirectiveType DirectiveKind { get; } = DirectiveType.Space;
        
        public new SpaceSyntax SetPos(Position startPos, int length)
        {
            base.SetPos(startPos, length);
            return this;
        }
    }
}