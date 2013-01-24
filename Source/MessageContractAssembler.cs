#region Copyright (c) 2006-2011 LOKAD SAS. All rights reserved

// You must not remove this notice, or any other, from this software.
// This document is the property of LOKAD SAS and must not be disclosed

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Antlr.Runtime;
using Antlr.Runtime.Tree;
using MessageContracts;

namespace Lokad.CodeDsl
{
    public sealed class MessageContractAssembler
    {
        static IEnumerable<Member> WalkContractMember(ITree tree, Context context)
        {
            if (tree.Type == MessageContractsLexer.FragmentReference)
            {
                var fragmentId = tree.GetChild(0).Text;

                if (!context.Fragments.ContainsKey(fragmentId))
                {
                    var errorNode = (CommonTree)tree;
                    throw new InvalidOperationException(string.Format("Line: {0}\r\nUnknown fragment: {1}", errorNode.Line, errorNode.Text));
                }

                var fragment = context.Fragments[fragmentId];
                yield return new Member(fragment.Type, fragment.Name, fragmentId);
                yield break;
            }
            if (tree.Type == MessageContractsLexer.MemberToken)
            {
                var type = tree.GetChild(0).Text;
                var name = tree.GetChild(1).Text;
                if (type == "ref")
                {
                    var match = context.Contracts.Where(c => c.Name == name).ToArray();
                    if (match.Length == 1)
                    {
                        foreach (var member in match[0].Members)
                        {
                            yield return member;
                        }
                        yield break;
                    }
                    throw new InvalidOperationException(string.Format("Unknown include '{0}'", name));
                }
                yield return (new Member(type, name, name));
                yield break;
            }
            if (tree.Type == MessageContractsLexer.StringRepresentationToken)
            {
                var text = tree.GetChild(0).Text;
                yield return new Member(null, text, null, Member.Kinds.StringRepresentation);
                yield break;
            }

            if (tree.Type == MessageContractsLexer.ComGuidToken)
            {
                var text = tree.GetChild(0).Text;
                yield return new Member(null, text, null, Member.Kinds.ComGuid);
                yield break;
            }

            if (tree.Type == MessageContractsLexer.ComInterfaceGuidToken)
            {
                var text = tree.GetChild(0).Text;
                yield return new Member(null, text, null, Member.Kinds.ComInterfaceGuid);
                yield break;
            }

            var node = (Antlr.Runtime.Tree.CommonErrorNode) tree;
            throw new InvalidOperationException(string.Format("Line: {0}\r\nUnknown fragment: {1}", node.start.Line, node.Text));
        }

        public void WalkDeclarations(ITree tree, Context context)
        {
            var t = tree;
            switch (t.Type)
            {
                case MessageContractsLexer.FragmentEntry:
                    var fragmentId = t.GetChild(0).Text;
                    var fragmentType = t.GetChild(1).Text;
                    var fragmentName = t.GetChild(2).Text;
                    context.Fragments[fragmentId] = new Fragment(fragmentType, fragmentName);
                    break;
                case MessageContractsLexer.ModifierDefinition:
                    var modifier = t.GetChild(0).Text;
                    context.CurrentEntity.Modifiers.Remove(modifier);
                    for (int i = 1; i < t.ChildCount; i++)
                    {
                        context.CurrentEntity.Modifiers.Add(modifier, t.GetChild(i).Text);
                    }
                    break;
                case MessageContractsLexer.TypeToken:
                    var name = t.GetChild(0).Text;
                    var block = t.GetChild(1);

                    var modifiers = new List<Modifier>();
                    for (int i = 2; i < t.ChildCount; i++)
                    {
                        var mod = t.GetChild(i).Text;
                        var used = context.CurrentEntity.Modifiers.GetValues(mod);
                        if (null == used || used.Length == 0)
                        {
                            var format = string.Format("Entity '{0}' does not have modifier reference: '{1}'",
                                context.CurrentEntity.Name, mod);
                            throw new InvalidOperationException(format);
                        }
                        foreach (var s in used)
                        {
                            modifiers.Add(new Modifier(mod, s));
                        }
                    }


                    var message = new Message(name, modifiers);
                    if (modifiers.Any())
                    {
                        // only commands and events have modifiers
                        foreach (var member in context.Entities.Peek().FixedMembers)
                        {
                            message.Members.Add(member);
                        }
                    }

                    var comClassGuid = "";
                    var comInterfaceGuid = "";

                    for (int i = 0; i < block.ChildCount; i++)
                    {
                        var members = WalkContractMember(block.GetChild(i), context).ToArray();
                        message.Members.AddRange(members.Where(m => m.Kind == Member.Kinds.Field));

                        var stringRepresentations =
                            members.Where(m => m.Kind == Member.Kinds.StringRepresentation).ToArray();
                        switch (stringRepresentations.Length)
                        {
                            case 0:
                                break;
                            case 1:
                                message.StringRepresentation = stringRepresentations[0].Name;
                                break;
                            default:
                                throw new InvalidOperationException("Only one string representation per message");
                        }

                        if (members.Any(m => m.Kind == Member.Kinds.ComGuid))
                            comClassGuid = members.First(m => m.Kind == Member.Kinds.ComGuid).Name;
                        if (members.Any(m => m.Kind == Member.Kinds.ComInterfaceGuid))
                            comInterfaceGuid = members.First(m => m.Kind == Member.Kinds.ComInterfaceGuid).Name;
                    }

                    if (!string.IsNullOrWhiteSpace(comClassGuid) && !string.IsNullOrWhiteSpace(comInterfaceGuid))
                    {
                        message.InteropData = new ComInteropData(comClassGuid, comInterfaceGuid);
                        message.Modifiers.Insert(0, new Modifier("",string.Format("I{0}",message.Name)));
                        context.Using.Add("System.Runtime.InteropServices");
                    }

                    context.Contracts.Add(message);
                    context.CurrentEntity.Messages.Add(message);
                    break;

                case MessageContractsLexer.EntityDefinition:
                    var entityName = t.GetChild(0).Text;
                    var entityBlock = t.GetChild(1);


                    var entity = new Entity(entityName);
                    for (int i = 0; i < entityBlock.ChildCount; i++)
                    {
                        entity.FixedMembers.AddRange(WalkContractMember(entityBlock.GetChild(i), context));
                    }
                    context.PushEntity(entity);

                    for (int i = 2; i < t.ChildCount; i++)
                    {
                        WalkDeclarations(t.GetChild(i), context);
                    }
                    break;
                case MessageContractsLexer.NamespaceToken:
                    var ns = string.Join(".", t.Children().Select(s => s.Text));
                    context.CurrentNamespace = ns;
                    break;
                case MessageContractsLexer.ExternToken:
                    var text = t.GetChild(0).Text;
                    context.CurrentExtern = text;
                    break;
                case MessageContractsLexer.UsingToken:
                    var us = string.Join(".", t.Children().Select(s => s.Text));
                    context.Using.Add(us);
                    break;
                default:
                    var node = (CommonErrorNode) t;
                    throw new InvalidOperationException(string.Format("Line: {0}\r\nUnexpected token: {1}", node.start.Line, node.Text));
            }
        }

        public Context From(string source)
        {
            var stream = new ANTLRStringStream(source);
            var lexer = new MessageContractsLexer(stream);

            var tokens = new CommonTokenStream(lexer);

            var parser = new MessageContractsParser(tokens)
            {
                TreeAdaptor = new CommonTreeAdaptor()
            };

            var program = parser.GetProgram();

            var commonTree = (CommonTree)program.Tree;

            var node = commonTree as CommonErrorNode;

            if (node != null)
            {
                throw new InvalidOperationException(node.ToString());
            }


            var ctx = new Context();

            foreach (var child in commonTree.Children)
            {
                WalkDeclarations(child, ctx);
            }
            return ctx;
        }
    
    }

    public static class Extend
    {
        public static IEnumerable<ITree> Children(this ITree self)
        {
            for (int i = 0; i < self.ChildCount; i++)
            {
                yield return self.GetChild(i);
            }
        } 
    }
}

namespace MessageContracts
{
    public partial class MessageContractsParser
    {
        public AstParserRuleReturnScope<object, IToken> GetProgram()
        {
            return program();
        }
    }

    public partial class MessageContractsLexer
    {
        public const int HIDDEN = Hidden;
    }
}