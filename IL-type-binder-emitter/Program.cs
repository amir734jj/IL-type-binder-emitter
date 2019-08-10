using System;
using System.Collections.Generic;
using Xunit;

namespace IL_type_binder_emitter
{
    public class Entity
    {
        public string Name { get; set; }
    }

    public interface ICommon
    {
        string Name { get; set; }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            var table = new Dictionary<string, (Type, string)>
            {
                ["Name"] = (typeof(string), "Name")
            };

            var type = new CustomTypeGenerator<Entity, ICommon>(table).EmittedType;

            var entity = new Entity {Name = "Amir"};

            var rslt = (ICommon) Activator.CreateInstance(type, entity);

            Assert.Equal(rslt.Name, "Amir");

            rslt.Name = "Taha";

            Assert.Equal(rslt.Name, "Taha");
        }
    }
}