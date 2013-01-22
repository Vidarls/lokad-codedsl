﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lokad.CodeDsl
{
    public sealed class TemplatedGenerator
    {
        public string ClassNameTemplate { get; set; }
        public string MemberTemplate { get; set; }
        
        public string Region { get; set; }
        public string GenerateInterfaceForEntityWithModifiers { get; set; }
        public string TemplateForInterfaceName { get; set; }
        public string TemplateForInterfaceMember { get; set; }
        public TemplatedGenerator()
        {
            Region = "Generated by Lokad Code DSL";
            ClassNameTemplate = @"
[ProtoContract]
public sealed class {0}";

            MemberTemplate = @"[ProtoMember({0})] public readonly {1} {2};";

            TemplateForInterfaceName = "public interface I{0}";
            TemplateForInterfaceMember = "void When({0} c)";
            GenerateInterfaceForEntityWithModifiers = "none";
            //
        }

        public void Generate(Context context, IndentedTextWriter outer)
        {
            var writer = new CodeWriter(outer);

            
            foreach (var source in context.Using.Distinct().OrderBy(s => s))
            {
                writer.WriteLine("using {0};", source);
            }

            writer.WriteLine(@"
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Local");


            writer.WriteLine("namespace {0}", context.CurrentNamespace);
            writer.WriteLine("{");

            writer.Indent += 1;

            if (!string.IsNullOrEmpty(Region))
            {
                writer.WriteLine("#region {0}", Region);
            }

            WriteContext(writer, context);


            if (!string.IsNullOrEmpty(Region))
            {
                writer.WriteLine("#endregion");
            }

            writer.Indent -= 1;
            writer.WriteLine("}");
        }

        private void WriteContext(CodeWriter writer, Context context)
        {
            foreach (var contract in context.Contracts)
            {
                if (contract.InteropData != null)
                    WriteInteropData(writer, contract);

                writer.Write(ClassNameTemplate, contract.Name, context.CurrentExtern);

                if (contract.Modifiers.Any())
                    writer.Write(" : {0}", string.Join(", ", contract.Modifiers.Select(s => s.Interface).ToArray()));

                writer.WriteLine();

                writer.WriteLine("{");
                writer.Indent += 1;

                if (contract.Members.Count > 0)
                {
                    WriteMembers(contract, writer);

                    writer.WriteLine();
                    WritePrivateCtor(writer, contract);

                    writer.Write("public {0} (", contract.Name);
                    WriteParameters(contract, writer);
                    writer.WriteLine(")");
                    writer.WriteLine("{");

                    writer.Indent += 1;
                    WriteAssignments(contract, writer);
                    writer.Indent -= 1;

                    writer.WriteLine("}");

                }
                WriteToString(writer, contract);
                writer.Indent -= 1;
                writer.WriteLine("}");
            }
            foreach (var entity in context.Entities)
            {
                if ((entity.Name ?? "default") == "default")
                    continue;

                GenerateEntityInterface(entity, writer, "?", "public interface I{0}ApplicationService");
                GenerateEntityInterface(entity, writer, "!", "public interface I{0}State");
            }
        }

        private void WriteInteropData(CodeWriter writer, Message contract)
        {
            writer.WriteLine("public partial class MessageFactory");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.Write("public I{0} Create{0} (", contract.Name);
            WriteParameters(contract, writer);
            writer.WriteLine(")");
            writer.WriteLine("{");
            writer.Indent += 1;
            writer.Write("return new {0}(", contract.Name);
            WriteFactoryParamteres(contract, writer);
            writer.WriteLine(");");
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.WriteLine();

            writer.WriteLine("[ComVisible(true)]");
            writer.WriteLine(contract.InteropData.ComInterfaceGuidAttribute);
            writer.WriteLine("public partial interface I{0}", contract.Name);
            writer.WriteLine("{");
            writer.Indent += 1;
            if (contract.Members.Count > 0)
            {
                WriteInterfaceMembers(contract, writer);
            }
            writer.Indent -= 1;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.WriteLine("[ComVisible(true)]");
            writer.WriteLine(contract.InteropData.ClassGuidAttribute);
            writer.WriteLine("[ClassInterface(ClassInterfaceType.None)]");
        }

        static void WritePrivateCtor(CodeWriter writer, Message contract)
        {
            var arrays = contract.Members.Where(p => p.Type.EndsWith("[]")).ToArray();
            if (!arrays.Any())
            {
                writer.WriteLine(@"{0} () {{}}", contract.Name);
            }
            else
            {
                writer.WriteLine(@"{0} () 
{{", contract.Name);
                writer.Indent += 1;
                foreach (var array in arrays)
                {
                    writer.WriteLine("{0} = new {1};",
                        GeneratorUtil.MemberCase(array.Name),
                        array.Type.Replace("[]", "[0]")
                        );
                }
                writer.Indent -= 1;
                writer.WriteLine("}");
            }
        }

        static void WriteToString(CodeWriter writer, Message contract)
        {
            if (string.IsNullOrWhiteSpace(contract.StringRepresentation))
                return;

            writer.WriteLine();
            writer.WriteLine("public override string ToString()");
            writer.WriteLine("{");
            writer.Indent += 1;

            var text = contract.StringRepresentation;
            var active = new List<string>();
            foreach (var member in contract.Members)
            {
                text = ReplaceAdd(text, "{" + member.DslName + ":", "{" + active.Count + ":", active, member);
                text = ReplaceAdd(text, "{" + member.DslName + "}", "{" + active.Count + "}", active, member);
                

                if (member.DslName != member.Name)
                {
                    text = ReplaceAdd(text, "{" + member.Name + ":", "{" + active.Count + ":", active, member);
                    text = ReplaceAdd(text, "{" + member.Name + "}", "{" + active.Count + "}", active, member);
                }
            }

            writer.Write("return string.Format(@{0}", text);

            foreach (var variable in active)
            {
                writer.Write(", " + GeneratorUtil.MemberCase(variable));
            }
            writer.WriteLine(");");
            writer.Indent -= 1;
            writer.WriteLine("}");
        }

        static string ReplaceAdd(string text, string v1, string to1, List<string> active, Member member)
        {
            if (text.IndexOf(v1, StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                text = ReplaceString(text, v1, to1);
                active.Add(member.Name);
            }
            return text;
        }

        static public string ReplaceString(string str, string oldValue, string newValue)
        {
            var comparison = StringComparison.InvariantCultureIgnoreCase;
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        void GenerateEntityInterface(Entity entity, CodeWriter writer, string member, string template)
        {
            var ms = member.Split(',');
            var matches = entity.Messages.Where(m => m.Modifiers.Select(s => s.Identifier).Intersect(ms).Any()).ToList();
            if (matches.Any())
            {
                writer.WriteLine();
                writer.WriteLine(template, entity.Name);
                writer.WriteLine("{");
                writer.Indent += 1;
                foreach (var contract in matches)
                {
                    writer.WriteLine("void When({0} {1});", contract.Name, member == "!" ? "e" : "c");
                }
                writer.Indent -= 1;
                writer.WriteLine("}");
            }
        }

        void WriteInterfaceMembers(Message message, CodeWriter writer)
        {
            foreach (var member in message.Members)
            {
                writer.WriteLine("{0} {1} {{ get; }}", member.Type,GeneratorUtil.MemberCase(member.Name));
            }
        }

        void WriteMembers(Message message, CodeWriter writer)
        {
            var idx = 1;
            foreach (var member in message.Members)
            {
                writer.WriteLine(MemberTemplate, idx, member.Type, GeneratorUtil.MemberCase(member.Name));


                idx += 1;
            }
        }

        void WriteFactoryParamteres(Message message, CodeWriter writer)
        {
            var separator = "";
            
            foreach (var member in message.Members)
            {
                writer.Write("{0}{1}",separator, GeneratorUtil.ParameterCase(member.Name));
                separator = ", ";
            }
        }
    
        void WriteParameters(Message message, CodeWriter writer)
        {
            var first = true;
            foreach (var member in message.Members)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    writer.Write(", ");
                }
                writer.Write("{0} {1}", member.Type, GeneratorUtil.ParameterCase(member.Name));
            }
        }

        void WriteAssignments(Message message, CodeWriter writer)
        {
            foreach (var member in message.Members)
            {
                writer.WriteLine("{0} = {1};", GeneratorUtil.MemberCase(member.Name), GeneratorUtil.ParameterCase(member.Name));
            }
        }
    }

    public sealed class CodeWriter
    {
        private readonly IndentedTextWriter _writer;

        public CodeWriter(IndentedTextWriter writer)
        {
            _writer = writer;
        }

        public int Indent { get { return _writer.Indent; } set { _writer.Indent = value; } }
        public void Write(string format, params object[] args)
        {
            var txt = string.Format(format, args);
            var lines = txt.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            

            for (int i = 0; i < lines.Length; i++)
            {
                bool thisIsLast = i == (lines.Length - 1);
                if (thisIsLast)
                    _writer.Write(lines[i]);
                else
                    _writer.WriteLine(lines[i]);

            }
        }

        public void WriteLine()
        {
            _writer.WriteLine();
        }

        public void WriteLine(string format, params object[] args)
        {
            
            var txt = args.Length == 0 ? format : string.Format(format, args);
            var lines = txt.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            

            foreach (string t in lines)
            {
                _writer.WriteLine(t);
            }
        }
    }
}