using System.Collections.Generic;
using System.Collections.Specialized;

namespace Lokad.CodeDsl
{
    public sealed class Context
    {
        public IDictionary<string, Fragment> Fragments = new Dictionary<string, Fragment>();
        public IList<Message> Contracts = new List<Message>();
        //public IDictionary<string,string> Modifiers = new Dictionary<string, string>();

        public Stack<Entity> Entities { get; set; }

        public void PushEntity(Entity entity)
        {
            Entities.Push(entity);

        }

        public Entity CurrentEntity { get { return Entities.Peek(); } }

        public string CurrentNamespace = "Lokad.Contracts";
        public string CurrentExtern = "Lokad.Contracts";
        public IList<string> Using; 

        public Context()
        {
            Entities = new Stack<Entity>();

            Using = new List<string>()
                {
                    "System","System.Collections.Generic",
                    "System.Runtime.Serialization"
                };

            var entity = new Entity("default");
            entity.Modifiers.Add("?", "ICommand");
            entity.Modifiers.Add("!", "IEvent");
            Entities.Push(entity);
        }
    }

    public sealed class Entity
    {
        public string Name { get; set; }
        public List<Member> FixedMembers { get; set; }
        public List<Message> Messages { get; set; }
        public NameValueCollection Modifiers { get; set; }
        public string Namespace;
        

        public Entity(string name)
        {
            Name = name;
            FixedMembers = new List<Member>();
            Messages = new List<Message>();
            Modifiers = new NameValueCollection();
        }
    }

    public sealed class Fragment
    {
        public readonly string Type;
        public readonly string Name;

        public Fragment(string type, string name)
        {
            Type = type;
            Name = name;
        }
    }


    public sealed class Member
    {
        public readonly string Name;
        public readonly string Type;
        public readonly Kinds Kind;
        public readonly string DslName;
        public Member(string type, string name, string dslName, Kinds kind = Kinds.Field)
        {
            Name = name;
            Type = type;
            Kind = kind;
            DslName = dslName;
        }

        public enum Kinds
        {
            Field, StringRepresentation, ComGuid, ComInterfaceGuid
        }
    }

    public sealed class Message
    {
        public readonly string Name;
        public readonly IList<Modifier> Modifiers;

        public Message(string name, IList<Modifier> modifiers)
        {
            Name = name;
            Modifiers = modifiers;
        }

        public string StringRepresentation;

        public ComInteropData InteropData;

        public List<Member> Members = new List<Member>();
    }

    public sealed class ComInteropData
    {
        public readonly string ClassGuidAttribute;
        public readonly string ComInterfaceGuidAttribute;

        public ComInteropData(string classGuidAttribute, string comInterfaceGuidAttribute)
        {
            ClassGuidAttribute = classGuidAttribute;
            ComInterfaceGuidAttribute = comInterfaceGuidAttribute;
        }
    }

    public sealed class Modifier
    {
        public readonly string Identifier;
        public readonly string Interface;

        public Modifier(string identifier, string @interface)
        {
            Identifier = identifier;
            Interface = @interface;
        }
    }


}