using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarn.Analysis
{
    /// <summary>
    /// Contains extension methods for producing <see cref="BasicBlock"/>
    /// objects from a Node.
    /// </summary>
    public static class InstructionCollectionExtensions
    {
        /// <summary>
        /// Produces <see cref="BasicBlock"/> objects from a Node.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static IEnumerable<BasicBlock> GetBasicBlocks(this Node node)
        {
            // If we don't have any instructions, return an empty collection
            if (node == null || node.Instructions == null || node.Instructions.Count == 0)
            {
                return Enumerable.Empty<BasicBlock>();
            }

            var result = new List<BasicBlock>();

            var leaderIndices = new HashSet<int>
            {
                // The first instruction is a leader.
                0,
            };

            foreach (var label in node.Labels)
            {
                // If the instruction is labelled (i.e. it is the target of a
                // jump), it is a leader.
                leaderIndices.Add(label.Value);
            }

            for (int i = 0; i < node.Instructions.Count; i++)
            {
                // Every instruction after a jump (conditional or
                // nonconditional) is a leader.
                switch (node.Instructions[i].Opcode)
                {
                    case Instruction.Types.OpCode.JumpTo:
                    case Instruction.Types.OpCode.Jump:
                    case Instruction.Types.OpCode.JumpIfFalse:
                    case Instruction.Types.OpCode.Stop:
                    case Instruction.Types.OpCode.RunNode:
                        leaderIndices.Add(i + 1);
                        break;
                    default:
                        // nothing to do
                        break;
                }
            }

            // Now that we know what the leaders are, run through the
            // instructions; every time we encounter a leader, start a new basic
            // block.
            var currentBlockInstructions = new List<Instruction>();

            int lastLeader = 0;

            for (int i = 0; i < node.Instructions.Count; i++)
            {
                // The current instruction is a leader! If we have accumulated
                // instructions, create a new block from them, store it, and
                // start a new list of instructions.
                if (leaderIndices.Contains(i))
                {
                    if (currentBlockInstructions.Count > 0)
                    {
                        var block = new BasicBlock
                        {
                            NodeName = node.Name,
                            Instructions = new List<Instruction>(currentBlockInstructions),
                            FirstInstructionIndex = lastLeader,
                            LabelName = GetLabelNameForInstructionIndex(lastLeader),
                        };
                        result.Add(block);
                    }

                    lastLeader = i;
                    currentBlockInstructions.Clear();
                }

                // Add the current instruction to our current accumulation.
                currentBlockInstructions.Add(node.Instructions[i]);
            }

            // We've reached the end of the instruction list. If we have any
            // accumulated instructions, create a final block here.
            if (currentBlockInstructions.Count > 0)
            {
                var block = new BasicBlock
                {
                    NodeName = node.Name,
                    Instructions = new List<Instruction>(currentBlockInstructions),
                    FirstInstructionIndex = lastLeader,
                    LabelName = GetLabelNameForInstructionIndex(lastLeader),
                };
                result.Add(block);
            }

            BasicBlock GetBlock(string label)
            {
                var index = node.Labels[label];

                try
                {
                    return result.First(block => block.FirstInstructionIndex == index);
                }
                catch (System.InvalidOperationException)
                {
                    // nothing found
                    throw new System.InvalidOperationException($"No block in {node.Name} starts at index {index}");
                }
            }
            BasicBlock GetBlockWithIndex(int index)
            {
                try
                {
                    return result.First(block => block.FirstInstructionIndex == index);
                }
                catch (System.InvalidOperationException)
                {
                    // nothing found
                    throw new System.InvalidOperationException($"No block in {node.Name} starts at index {index}");
                }
            }

            // Given an instruction index, returns the name of the label for
            // this index, or null if the instruction doesn't have a label.
            string GetLabelNameForInstructionIndex(int index)
            {
                return node.Labels.FirstOrDefault(pair => pair.Value == index).Key;
            }

            // Final pass: now that we have all the blocks, go through each of
            // them and build the links between them
            foreach (var block in result)
            {
                var optionDestinations = new List<string>();
                string currentStringAtTopOfStack = null;
                int count = 0;
                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.Opcode)
                    {
                        case Instruction.Types.OpCode.AddOption:
                            {
                                // Track the destination that this instruction says
                                // it'll jump to. It'll either be to a named label, or
                                // to a node.
                                var destinationNodeName = instruction.Operands.ElementAt(1);
                                optionDestinations.Add(destinationNodeName.StringValue);
                                break;
                            }
                        case Instruction.Types.OpCode.Jump:
                            {
                                // We're jumping to a labeled section of the same node.
                                foreach (var destinationLabel in optionDestinations)
                                {
                                    var destinationBlock = GetBlock(destinationLabel);

                                    block.AddDestination(destinationBlock, BasicBlock.Condition.Option);
                                }
                                break;
                            }
                        case Instruction.Types.OpCode.JumpTo:
                            {
                                var destinationBlock = GetBlock(instruction.Operands.ElementAt(0).StringValue);
                                block.AddDestination(destinationBlock, BasicBlock.Condition.DirectJump);
                                break;
                            }
                        case Instruction.Types.OpCode.PushString:
                            {
                                // The top of the stack is now a string. (This
                                // isn't perfect, because it doesn't handle
                                // stuff like functions, which modify the stack,
                                // but the most common case is <<jump
                                // NodeName>>, which is a combination of 'push
                                // string' followed by 'run node at top of
                                // stack')
                                currentStringAtTopOfStack = instruction.Operands.ElementAt(0).StringValue;
                                break;
                            }
                        case Instruction.Types.OpCode.PushBool:
                        case Instruction.Types.OpCode.PushFloat:
                        case Instruction.Types.OpCode.PushVariable:
                            {
                                // The top of the stack is now no longer a
                                // string. Again, not a fully accurate
                                // representation of what's going on, but for
                                // the moment, we're not supporting 'jump to
                                // expression' here.
                                currentStringAtTopOfStack = null;
                                break;
                            }
                        case Instruction.Types.OpCode.RunNode:
                            {
                                if (currentStringAtTopOfStack != null)
                                {
                                    block.AddDestination(currentStringAtTopOfStack, BasicBlock.Condition.DirectJump);
                                }
                                break;
                            }
                        case Instruction.Types.OpCode.JumpIfFalse:
                            {
                                var destinationLabel = instruction.Operands.ElementAt(0).StringValue;

                                var destinationFalseBlock = GetBlock(destinationLabel);
                                var destinationTrueBlock = GetBlockWithIndex(block.FirstInstructionIndex + count + 1);

                                block.AddDestination(destinationFalseBlock, BasicBlock.Condition.ExpressionIsFalse);
                                block.AddDestination(destinationTrueBlock, BasicBlock.Condition.ExpressionIsTrue);
                                break;
                            }
                    }
                    count += 1;
                }

                if (block.Destinations.Count() == 0)
                {
                    // We've reached the end of this block, and don't have any
                    // destinations. If our last destination isn't 'stop', then
                    // we'll fall through to the next node.
                    if (block.Instructions.Last().Opcode != Instruction.Types.OpCode.Stop)
                    {
                        var nextBlockStartInstruction = block.FirstInstructionIndex + block.Instructions.Count();

                        var destination = GetBlockWithIndex(nextBlockStartInstruction);
                        block.AddDestination(destination, BasicBlock.Condition.Fallthrough);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// A basic block is a run of instructions inside a Node. Basic blocks group
    /// instructions up into segments such that execution only ever begins at
    /// the start of a block (that is, a program never jumps into the middle of
    /// a block), and execution only ever leaves at the end of a block.
    /// </summary>
    public class BasicBlock
    {
        /// <summary>
        /// Gets the name of the label that this block begins at, or null if this basic block does not begin at a labelled instruction.
        /// </summary>
        public string LabelName { get; set; }

        /// <summary>
        /// Gets the name of the node that this block is in.
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// Gets the index of the first instruction of the node that this block is in.
        /// </summary>
        public int FirstInstructionIndex { get; set; }

        /// <summary>
        /// Gets a descriptive name for the block.
        /// </summary>
        /// <remarks>
        /// If this block begins at a labelled instruction, the name will be <c>[NodeName].[LabelName]</c>. Otherwise, it will be <c>[NodeName].[FirstInstructionIndex]</c>.
        /// </remarks>
        public string Name 
        {
            get 
            {
                if (LabelName != null) 
                {
                    return $"{NodeName}.{LabelName}";
                }
                else
                {
                    return $"{NodeName}.{FirstInstructionIndex}";
                }
            }
        }

        /// <summary>
        /// Get the ancestors of this block - that is, blocks that may run immediately before this block.
        /// </summary>
        public IEnumerable<BasicBlock> Ancestors => ancestors;

        /// <summary>
        /// Gets the destinations of this block - that is, blocks or nodes that
        /// may run immediately after this block.
        /// </summary>
        /// <seealso cref="Destination"/>
        public IEnumerable<Destination> Destinations => destinations;

        /// <summary>
        /// Gets the Instructions that form this block.
        /// </summary>
        public IEnumerable<Instruction> Instructions { get; set; } = new List<Instruction>();

        /// <summary>
        /// Adds a new destination to this block, that points to another block.
        /// </summary>
        /// <param name="descendant">The new descendant node.</param>
        /// <param name="condition">The condition under which <paramref
        /// name="descendant"/> will be run.</param>
        /// <exception cref="ArgumentNullException">Thrown when descendant is
        /// <see langword="null"/>.</exception>
        public void AddDestination(BasicBlock descendant, Condition condition)
        {
            if (descendant is null)
            {
                throw new ArgumentNullException(nameof(descendant));
            }

            destinations.Add(new Destination { Type = Destination.DestinationType.Block, Block = descendant, Condition = condition });
            descendant.ancestors.Add(this);
        }

        /// <summary>
        /// Adds a new destination to this block, that points to a node.
        /// </summary>
        /// <param name="nodeName">The name of the destination node.</param>
        /// <param name="condition">The condition under which <paramref
        /// name="descendant"/> will be run.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref
        /// name="nodeName"/> is <see langword="null"/>.</exception>
        public void AddDestination(string nodeName, Condition condition)
        {
            if (string.IsNullOrEmpty(nodeName))
            {
                throw new ArgumentException($"'{nameof(nodeName)}' cannot be null or empty.", nameof(nodeName));
            }

            destinations.Add(new Destination { Type = Destination.DestinationType.Node, NodeName = nodeName, Condition = condition });
        }

        private HashSet<BasicBlock> ancestors = new HashSet<BasicBlock>();

        private HashSet<Destination> destinations = new HashSet<Destination>();

        /// <summary>
        /// A destination represents a <see cref="BasicBlock"/> or node that may
        /// be run, following the execution of a <see cref="BasicBlock"/>.
        /// </summary>
        /// <remarks>
        /// Destination objects represent links between blocks, or between
        /// blocks and nodes.
        /// </remarks>
        public struct Destination
        {
            /// <summary>
            /// The type of a Destination.
            /// </summary>
            public enum DestinationType
            {
                Node,
                Block,
            }

            /// <summary>
            /// Gets the Destination's type - whether the destination is a
            /// block, or a node.
            /// </summary>
            public DestinationType Type { get; set; }

            /// <summary>
            /// The name of the node that this destination refers to.
            /// </summary>
            /// <remarks>This value is only valid when <see cref="Type"/> is
            /// <see cref="DestinationType.Node"/>.</remarks>
            public string NodeName { get; set; }

            /// <summary>
            /// The block that this destination refers to.
            /// </summary>
            /// <remarks>This value is only valid when <see cref="Type"/> is
            /// <see cref="DestinationType.Block"/>.</remarks>
            public BasicBlock Block { get; set; }

            /// <summary>
            /// The condition that causes this destination to be reached.
            /// </summary>
            public Condition Condition { get; set; }
        }

        /// <summary>
        /// Gets all descendants (that is, destinations, and destinations of
        /// those destinations, and so on), recursively.
        /// </summary>
        /// <remarks>
        /// Cycles are detected and avoided.
        /// </remarks>
        public IEnumerable<BasicBlock> Descendants
        {
            get 
            {
                // Start with a queue of immediate children that link to blocks
                Queue<BasicBlock> candidates = new Queue<BasicBlock>(this.Destinations.Where(d => d.Block != null).Select(d=> d.Block));

                List<BasicBlock> descendants = new List<BasicBlock>();

                while (candidates.Count > 0)
                {
                    var next = candidates.Dequeue();
                    if (descendants.Contains(next)) 
                    {
                        // We've already seen this one - skip it.
                        continue;
                    }
                    descendants.Add(next);
                    foreach (var destination in next.Destinations.Where(d => d.Block != null).Select(d=> d.Block))
                    {
                        candidates.Enqueue(destination);
                    }
                }

                return descendants;

            }
        }

        /// <summary>
        /// Gets all descendants (that is, destinations, and destinations of
        /// those destinations, and so on) that have any player-visible content,
        /// recursively.
        /// </summary>
        /// <remarks>
        /// Cycles are detected and avoided.
        /// </remarks>
        public IEnumerable<BasicBlock> DescendantsWithPlayerVisibleContent
        {
            get
            {
                return Descendants.Where(d => d.PlayerVisibleContent.Any());
            }
        }

        /// <summary>
        /// The conditions under which a <see cref="Destination"/> may be
        /// reached at the end of a BasicBlock.
        /// </summary>
        public enum Condition
        {
            /// <summary>
            /// The Destination is reached because the preceding BasicBlock
            /// reached the end of its execution, and the Destination's target
            /// is the block immediately following.
            /// </summary>
            Fallthrough,

            /// <summary>
            /// The Destination is reached beacuse of an explicit instruction to
            /// go to this block.
            /// </summary>
            DirectJump,

            /// <summary>
            /// The Destination is reached because an expression evaluated to
            /// true.
            /// </summary>
            ExpressionIsTrue,

            /// <summary>
            /// The Destination is reached because an expression evaluated to
            /// false.
            /// </summary>
            ExpressionIsFalse,

            /// <summary>
            /// The Destination is reached because the player made an in-game
            /// choice to go to it.
            /// </summary>
            Option,
        }

        /// <summary>
        /// An abstract class that represents some content that is shown to the
        /// player.
        /// </summary>
        /// <remarks>
        /// This class is used, rather than the runtime classes Yarn.Line or
        /// Yarn.OptionSet, because when the program is being analysed, no
        /// values for any substitutions are available. Instead, these classes
        /// represent the data that is available offline.
        /// </remarks>
        public abstract class PlayerVisibleContentElement
        {
        }

        /// <summary>
        /// A line of dialogue that should be shown to the player.
        /// </summary>
        public class LineElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// The string table ID of the line that will be shown to the player.
            /// </summary>
            public string LineID;
        }

        /// <summary>
        /// A collection of options that should be shown to the player.
        /// </summary>
        public class OptionsElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// Represents a single option that may be presented to the player.
            /// </summary>
            public struct Option
            {
                /// <summary>
                /// The string table ID that will be shown to the player.
                /// </summary>
                public string LineID;

                /// <summary>
                /// The destination that will be run if this option is selected
                /// by the player.
                /// </summary>
                /// <remarks>
                /// This will be the name of a label, or the name of a node.
                /// </remarks>
                public string Destination;
            }

            /// <summary>
            /// The collection of options that will be delivered to the player.
            /// </summary>
            public IEnumerable<Option> Options;
        }

        /// <summary>
        /// A command that will be executed.
        /// </summary>
        public class CommandElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// The text of the command.
            /// </summary>
            public string CommandText;
        }

        /// <summary>
        /// Gets the collection of player-visible content that will be delivered
        /// when this block is run.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Player-visible content means lines, options and commands. When this
        /// block is run, the entire contents of this collection will be
        /// displayed to the player, in the same order as they appear in this
        /// collection.
        /// </para>
        /// <para>
        /// If this collection is empty, then the block contains no visible
        /// content. This is the case for blocks that only contain logic, and do
        /// not contain any lines, options or commands.
        /// </para>
        /// <example>
        /// To tell the difference between the different kinds of content, use
        /// the <see langword="is"/> operator to check the type of each item:
        /// <code>
        /// foreach (var item in block.PlayerVisibleContent) { if (item is
        /// LineElement line) { // Do something with line } }
        /// </code>
        /// </example>
        /// </remarks>
        public IEnumerable<PlayerVisibleContentElement> PlayerVisibleContent
        {
            get
            {
                var accumulatedOptions = new List<(string LineID, string Destination)>();
                foreach (var instruction in Instructions)
                {
                    switch (instruction.Opcode)
                    {
                        case Instruction.Types.OpCode.RunLine:
                            yield return new LineElement
                            {
                                LineID = instruction.Operands[0].StringValue
                            };
                            break;

                        case Instruction.Types.OpCode.RunCommand:
                            yield return new CommandElement
                            {
                                CommandText = instruction.Operands[0].StringValue
                            };
                            break;

                        case Instruction.Types.OpCode.AddOption:
                            accumulatedOptions.Add((instruction.Operands[0].StringValue, instruction.Operands[1].StringValue));
                            break;

                        case Instruction.Types.OpCode.ShowOptions:
                            yield return new OptionsElement
                            {
                                Options = accumulatedOptions.Select(o => new OptionsElement.Option
                                {
                                    Destination = o.Destination,
                                    LineID = o.LineID,
                                })
                            };
                            accumulatedOptions.Clear();
                            break;
                    }
                }
            }
        }
    }
}