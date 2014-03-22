using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;

namespace XmlStreamProcessor.Test.UnitTest
{

	[TestFixture]
	class Test1
	{
		[Test]
		public void GenericTest()
		{
			var source = 
			@"
				<sample2 attr1='z'>
					<a/>
					<a><b/></a>
					<a/>
					<someint>1</someint>
					<somestring>asd</somestring>
					<subclasses attr2='x'>
						<subclass/>
						<subclass>
							<someint>2</someint>
							<subsubclass>
								<somestring>zzz</somestring>
							</subsubclass>
						</subclass>
						<subanotherclass>
							<someint>3</someint>
							<subsubclass></subsubclass>
						</subanotherclass>
						<subclass>
						</subclass>
					</subclasses>
				</sample2>
			";

			Func<XmlReader, Sample> parser = ParserTool.CreateParser(q => q.Type("sample2",
				a => new Sample()
				{
					Tag = a.Tag(),
					SomeInt = a.Int("someint"),
					SomeString = a.String("somestring"),
					Subclasses = a.Array("subclasses",
						b => b.Type("*",
							c => new Subclass(c.Int("someint"))
							{
								Tag = c.Tag(),
								SubSubclass = c.Type("subsubclass",
									d => new SubSubclass()
									{
										SomeString = d.String("somestring"),
										Tag = d.Tag(),
									}),
							})
						).ToList(),
				})
			); 
			
			var reader = XmlReader.Create(new StringReader(source));
			Sample result = parser(reader);

			var correct = new Sample()
			{
				SomeInt = 1,
				SomeString = "asd",
				Tag = "sample2",
				Subclasses = new List<Subclass>
				{
					new Subclass(0)
					{
						Tag = "subclass"
					},
					new Subclass(2)
					{
						SubSubclass = new SubSubclass() { SomeString = "zzz", Tag = "subsubclass" },
						Tag = "subclass"
					},
					new Subclass(3)
					{
						Tag = "subanotherclass",
						SubSubclass = new SubSubclass() { Tag = "subsubclass" },
					},
					new Subclass(0)
					{
						Tag = "subclass"
					},
				},
			};

			Assert.AreEqual(correct, result);
		}

		[Test]
		public void TestCompositeParser()
		{
			var source =
			@"
				<sample2 attr1='z'>
					<a/>
					<a><b/></a>
					<a/>
					<someint>1</someint>
					<somestring>asd</somestring>
					<subclasses attr2='x'>
						<subclass/>
						<subclass>
							<someint>2</someint>
							<subsubclass>
								<somestring>zzz</somestring>
							</subsubclass>
						</subclass>
						<subanotherclass>
							<someint>3</someint>
							<subsubclass></subsubclass>
						</subanotherclass>
						<subclass>
						</subclass>
					</subclasses>
				</sample2>
			";

			var subsubclassParser = ParserTool.CreateParser(
			a => new SubSubclass()
			{
				SomeString = a.String("somestring"),
				Tag = a.Tag(),
			});

			var subclassArrayParser = ParserTool.CreateArrayParser(
				y => y.Type("*", (z) => new Subclass(z.Int("someint"))
				{
					Tag = z.Tag(),
					SubSubclass = z.Custom("subsubclass", subsubclassParser),
				})
			);

			var parser = ParserTool.CreateParser((q) => q.Type("sample2",
				x => new Sample()
				{
					Tag = x.Tag(),
					SomeInt = x.Int("someint"),
					SomeString = x.String("somestring"),
					Subclasses = x.Custom("subclasses", subclassArrayParser).ToList(),
				})
			);

			var reader = XmlReader.Create(new StringReader(source));
			var result = parser(reader);

			var correct = new Sample()
			{
				SomeInt = 1,
				SomeString = "asd",
				Tag = "sample2",
				Subclasses = new List<Subclass>
				{
					new Subclass(0)
					{
						Tag = "subclass"
					},
					new Subclass(2)
					{
						SubSubclass = new SubSubclass() { SomeString = "zzz", Tag = "subsubclass" },
						Tag = "subclass"
					},
					new Subclass(3)
					{
						Tag = "subanotherclass",
						SubSubclass = new SubSubclass() { Tag = "subsubclass" },
					},
					new Subclass(0)
					{
						Tag = "subclass"
					},
				},
			};

			Assert.AreEqual(correct, result);
		}

		[Test]
		public void TestAlternatives()
		{
			var parser = ParserTool.CreateParser<int?[]>(
				a => a.Array("items",
					c => c.NullInt("item1") ?? (c.NullInt("item2")*10)
				)
			);

			var source = 
			@"
				<items>
					<item1>1</item1>
					<item2>2</item2>
					<item1>3</item1>
				</items>
			";

			var reader = XmlReader.Create(new StringReader(source));
			var result = parser(reader);

			CollectionAssert.AreEqual(result, new[] { 1, 20, 3 });
		}

		[Test]
		public void TestAttributes()
		{
			Func<XmlReader, string> parser = ParserTool.CreateParser(
				a => a.Type("sample",
					c => c.Attribute("b") + c.Attribute("a")
				)
			);

			var source = 
			@"
				<sample a='zxc' b='qwe'>
					123
				</sample>
			";

			var reader = XmlReader.Create(new StringReader(source));
			var result = parser(reader);

			CollectionAssert.AreEqual(result, "qwezxc");
		}

		[Test]
		public void TestVariousContent()
		{
			var source =
			@"<?xml version='1.0'?>
				<!-- This is a sample XML document -->
				<!DOCTYPE Items [<!ENTITY number '123'>]>
				<Items>
				  <Item>Test with an entity: &number;</Item>
				  <Item>Test with a CDATA section <![CDATA[<456>]]> def</Item>
				  <Item>Test with a char entity: &#65;</Item>
				  <Item><more/>Test with a child element stuff</Item>
				  <Item>Test with a child element stuff <more/></Item>
				  <Item>Test with a child element <more/> stuff</Item>
				  <!-- Fourteen chars in this element.-->
				  <Item>1234567890ABCD</Item>
				</Items>
			";
			Func<XmlReader, string[]> parser = ParserTool.CreateParser(
				a => a.Array("Items",
					c => c.String("Item")
				)
			);

			var settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Parse };
			var reader = XmlReader.Create(new StringReader(source), settings);
			var result = parser(reader);

			CollectionAssert.AreEqual(result, new[]
				{
					"Test with an entity: 123",
					"Test with a CDATA section <456> def",
					"Test with a char entity: A",
					"<more />Test with a child element stuff",
					"Test with a child element stuff <more />",
					"Test with a child element <more /> stuff",
					"1234567890ABCD",
				});
		}

		[Test]
		public void TestInnerLambda()
		{
			string source =
			@"
				<root>
					<int>1</int>
				</root>
			";
			
			var parser = ParserTool.CreateParser(
				a => a.Type("root",
					b => ((Func<int>)(() => b.Int("int")))()
				)
			);

			var reader = XmlReader.Create(new StringReader(source));
			var result = parser(reader);

			Assert.AreEqual(result, 1);
		}
	}

}
