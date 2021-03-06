using System;
using System.Collections.Generic;
using System.Linq;
using KeyValueStorage.Interfaces;
using KeyValueStorage.Tools.Structured.Schema;
using KeyValueStorage.Tools.Utility.Relationships;
using Moq;
using NUnit.Framework;
using ServiceStack.Text;

namespace KeyValueStorage.Tools.UnitTests.Structured.Schema
{
	[TestFixture]
	public class ObjectTypeSchemaTests
	{
		private Dictionary<Type, string> _foreignTypeWithAliasesForA = new Dictionary<Type, string>() { { typeof(TestTypeB), "B" } };

		[Test]
		public void ShouldBuildCorrectly()
		{
			var objectSchema = _CreateObjectTypeSchema();
			Assert.AreEqual(typeof(TestTypeA), objectSchema.ObjectType);
			Assert.AreEqual(_foreignTypeWithAliasesForA.ToList().Dump(), objectSchema.ToList().Dump());
		}

		private ObjectTypeSchema _CreateObjectTypeSchema()
		{
            //need to copy dict for concurrency issues
			var objectSchema = new ObjectTypeSchema(typeof(TestTypeA), _foreignTypeWithAliasesForA.ToDictionary(i => i.Key, i => i.Value));
			return objectSchema;
		}

		[Test]
		public void ShouldAdd()
		{
			var objectSchema = _CreateObjectTypeSchema();
			objectSchema.Add(typeof(TestTypeC),"C");
			Assert.AreEqual(new Dictionary<Type, string>() { { typeof(TestTypeB), "B" }, { typeof(TestTypeC), "C" } }.ToList().Dump(),objectSchema.ToList().Dump());
		}

		[Test]
		public void ShouldAdd2()
		{
			var objectSchema = _CreateObjectTypeSchema();
			objectSchema.AddRelationship<TestTypeC>("C");
			Assert.AreEqual(new Dictionary<Type, string>() { { typeof(TestTypeB), "B" }, { typeof(TestTypeC), "C" } }.ToList().Dump(), objectSchema.ToList().Dump());
		}

		[Test]
		public void ShouldBuildKeyRelationships()
		{
			var objectSchema = _CreateObjectTypeSchema();
			IKVStore kvStore = new Mock<IKVStore>().Object;
		    Key key = "";

			var relationships = objectSchema.BuildKeyRelationships(kvStore, key);

			var expected = new List<KeyWithRelationship>()
			{new KeyWithRelationship(key, new KVForeignKeyStoreRelationshipProvider(kvStore,"B"))};
			Assert.AreEqual(expected.Select(s => s.Key), relationships.ToList().Select(s => s.Key));
		}
	}

	public class TestTypeA{}

	public class TestTypeB {}

	public class TestTypeC{}
}