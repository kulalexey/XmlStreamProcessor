This forward-only XML processor is a wrapper over XMLReader.

You can easily use this to write xml parser or scanner. It has low memory overhead in compare with XDocument and can replace it in many tasks. Using lambda expressions & magic under hood give to you many flexibility in using this tool. But of course it has some limitations described below.

## Getting started

```
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
```

## There is also limitations:
- Complex selectors are not implemented (XPath, etc.)
- You can't use statements in lambda: there is no way for parser to look into it, and it can't prepare data for it execution (this won't work: `a => { return a.Int("someint"); }`)
- You can't use INode parameter anyway except calling it methods (for example, pass it into function is bad idea: `a => NiceFunc(a)`)
- You can't parse some node in one expression with different parsers. Keep in mind: forward only parser can parse xml node only way. (this is a bad idea: `a => a.Int("somenode").ToString() + a.String("somenode")`)
- You can't affect on parsing way from expression. keep in mind: lambda executed after(!) all data been 
prepared. (this is a bad idea: `a => a.Tag() == "someint" ? a.Int("value").ToString() : a.String("value")`)