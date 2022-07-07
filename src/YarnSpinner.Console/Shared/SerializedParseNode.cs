namespace YarnSpinnerConsole
{
    using System.Collections.Generic;

    /// <summary>
    /// A data-only class that stores a subset of the information
    /// related to parse nodes, and designed to be serialized to JSON.
    /// A parse node is either a rule (which has child nodes), or a
    /// token.
    /// </summary>
    public class SerializedParseNode
    {
        /// <summary>
        /// Gets or sets the line number that this parse node begins on.
        /// </summary>
        /// <remarks>
        /// The first line number is 1.
        /// </remarks>
        public int Line { get; set; }

        /// <summary>
        /// Gets or sets the column number that this parse node begins on.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Gets or sets the name of the rule or token that this node
        /// represents.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the text of this token. If this node
        /// represents a rule, this property will be <see
        /// langword="null"/>.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the name of the channel that this token
        /// appeared on. If this node represents a rule, this property
        /// wil be <see langword="null"/>.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Gets or sets the children of this node (that is, the rules
        /// or tokens that make up this node.)
        /// </summary>
        public List<SerializedParseNode> Children { get; set; } = null;
    }
}