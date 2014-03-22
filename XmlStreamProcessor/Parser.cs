using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;

namespace XmlStreamProcessor
{
	interface IXmlParser
	{
		object Parse(XmlReader reader); 
	}

	internal class NullBoolParser : IXmlParser
	{
		public object Parse(XmlReader reader)
		{
			var str = reader.ReadString();
			if (string.IsNullOrEmpty(str))
				return null;
			return Convert.ToBoolean(str);
		}
	}

	internal class IntParser : IXmlParser
	{
		public object Parse(XmlReader reader)
		{
			return Convert.ToInt32(reader.ReadString());
		}
	}

	internal class StringParser : IXmlParser
	{
		public object Parse(XmlReader reader)
		{
			var nodeDepth = reader.Depth;

			var str = reader.ReadString();
			if (str == null)
				return null;

			//also read all tags inside as strings. is it cool idea?
			while (reader.Depth > nodeDepth)
			{
				str += reader.ReadOuterXml();
				str += reader.ReadString();
			}

			if (string.IsNullOrEmpty(str)) //tuning as in XDocument.Load parser?? not always work :(
				return "";

			return str;
		}
	}

	internal class NullIntParser : IXmlParser
	{
		public object Parse(XmlReader reader)
		{
			var str = reader.ReadString();
			if (string.IsNullOrEmpty(str))
				return null;
			return Convert.ToInt32(str);
		}
	}

	internal class NullDecimalParser : IXmlParser
	{
		static readonly NumberFormatInfo numberFormatInfo = new NumberFormatInfo();

		public object Parse(XmlReader reader)
		{
			var str = reader.ReadString();
			if (string.IsNullOrEmpty(str))
				return null;
			return Convert.ToDecimal(str.Replace(".", numberFormatInfo.NumberGroupSeparator));
		}
	}

	internal class NullDateParser : IXmlParser
	{
		public object Parse(XmlReader reader)
		{
			var str = reader.ReadString();
			if (string.IsNullOrEmpty(str))
				return null;
			return Convert.ToDateTime(str);
		}
	}

	internal interface IXmlNodeHandler
	{
		void HandleNode(string xmlNode, ElementSchema elementSchema, object value);
		void HandleTagName(string xmlNode);
		object GetResult();
		void HandleAttribute(AttributeSchema schema, string value);
	}

	internal abstract class BaseTypeParser : IXmlParser
	{
		protected Func<ParserData, object> Generator;
		protected MarkupLambdaTraverser SchemaBuilder;

		public BaseTypeParser(LambdaExpression expression)
		{
			SchemaBuilder = new MarkupLambdaTraverser();
			var convertedExpression = (LambdaExpression)SchemaBuilder.Visit(expression);
			Generator = (Func<ParserData, object>)convertedExpression.Compile();
		}

		protected abstract IXmlNodeHandler CreateNodeHandler();

		public object Parse(XmlReader reader)
		{
			var nodeHandle = CreateNodeHandler();

			int rootDepth;
			if (reader.NodeType == XmlNodeType.None)
				rootDepth = -1;
			else
				rootDepth = reader.Depth;

			if (reader.NodeType == XmlNodeType.Element)
			{
				nodeHandle.HandleTagName(reader.LocalName);

				if (reader.HasAttributes && SchemaBuilder.AttributeSchema.Count > 0)
				{
					while (reader.MoveToNextAttribute())
					{
						AttributeSchema schema;
						SchemaBuilder.AttributeSchema.TryGetValue(reader.LocalName, out schema);
						if (schema != null)
							nodeHandle.HandleAttribute(schema, reader.Value);
					}
				}
			}

			while (reader.Read())
			{
				if (reader.Depth <= rootDepth)
					break;

				if (reader.NodeType == XmlNodeType.Element)
				{
					if (reader.Depth == rootDepth + 1) //work only with current level. do not walk into unknown child tags
					{
						var xmlNode = reader.LocalName;

						ElementSchema elementSchema;
						SchemaBuilder.ElementSchema.TryGetValue(xmlNode, out elementSchema);
						if (elementSchema == null)
						{
							xmlNode = "*";
							SchemaBuilder.ElementSchema.TryGetValue(xmlNode, out elementSchema);
						}

						if (elementSchema != null)
						{
							var value = elementSchema.Parser.Parse(reader);
							nodeHandle.HandleNode(xmlNode, elementSchema, value);
						}
					}
				}
			}

			return nodeHandle.GetResult();
		}
	}

	class TypeParser : BaseTypeParser
	{
		class TypeParserNodeHandler : IXmlNodeHandler
		{
			private readonly TypeParser _parser;
			readonly ParserData _parserData;

			public TypeParserNodeHandler(TypeParser parser)
			{
				_parser = parser;
				_parserData = new ParserData(parser.SchemaBuilder.ArgLength);
			}

			public void HandleNode(string xmlNode, ElementSchema elementSchema, object value)
			{
				if (_parserData.Data[elementSchema.ParseToArgN] == null)
					_parserData.Data[elementSchema.ParseToArgN] = value;
			}

			public void HandleTagName(string xmlNode)
			{
				_parserData.TagName = xmlNode;
			}

			public object GetResult()
			{
				return _parser.Generator(_parserData);
			}

			public void HandleAttribute(AttributeSchema schema, string value)
			{
				_parserData.Data[schema.ParseToArgN] = value;
			}
		}

		public TypeParser(LambdaExpression expression)
			: base(expression)
		{
		}

		protected override IXmlNodeHandler CreateNodeHandler()
		{
			return new TypeParserNodeHandler(this);
		}
	}

	class ArrayParser : BaseTypeParser
	{
		class ArrayParserNodeHandler : IXmlNodeHandler
		{
			private readonly ArrayParser _parser;
			readonly ParserData _parserData;

			private List<object> _result = new List<object>();

			public ArrayParserNodeHandler(ArrayParser parser)
			{
				_parser = parser;
				_parserData = new ParserData(parser.SchemaBuilder.ArgLength);
			}

			public void HandleNode(string xmlNode, ElementSchema elementSchema, object value)
			{
				_parserData.Data[elementSchema.ParseToArgN] = value;
				_result.Add(_parser.Generator(_parserData));
				_parserData.Data[elementSchema.ParseToArgN] = null;
			}

			public void HandleTagName(string xmlNode)
			{
				_parserData.TagName = xmlNode;
			}

			public void HandleAttribute(AttributeSchema schema, string value)
			{
				_parserData.Data[schema.ParseToArgN] = value;
			}

			public object GetResult()
			{
				var array = _parser.ArrayConstructor(_result.Count);
				for (var i = 0; i < _result.Count; i++)
					array.SetValue(_result[i], i);
				return array;
			}

		}

		protected readonly Func<int, Array> ArrayConstructor;

		public ArrayParser(LambdaExpression expression)
			: base(expression)
		{
			var param = Expression.Parameter(typeof(int), "n");
			var createArrayLambda =
				Expression.Lambda(
					Expression.NewArrayBounds(expression.Body.Type, param),
					param);
			ArrayConstructor = (Func<int, Array>)createArrayLambda.Compile();
		}

		protected override IXmlNodeHandler CreateNodeHandler()
		{
			return new ArrayParserNodeHandler(this);
		}
	}

	class CustomParser : IXmlParser
	{
		private readonly Func<Func<XmlReader, object>> _parser;

		public CustomParser(Func<Func<XmlReader, object>> parser)
		{
			_parser = parser;
		}

		public object Parse(XmlReader reader)
		{
			return _parser()(reader);
		}
	}

	public class ParserTool
	{
		public static Func<XmlReader, T> CreateParser<T>(Expression<Func<INode, T>>  expression)
		{
			var rawResult = new TypeParser((LambdaExpression)expression);
			return reader => (T)rawResult.Parse(reader);
		}

		public static Func<XmlReader, T[]> CreateArrayParser<T>(Expression<Func<INode, T>> expression)
		{
			var rawResult = new ArrayParser((LambdaExpression)expression);
			return reader => (T[])rawResult.Parse(reader);
		}
	}

	class ElementSchema
	{
		public int ParseToArgN;
		public IXmlParser Parser;
	}

	class AttributeSchema
	{
		public int ParseToArgN;
	}

	class MarkupLambdaTraverser : ExpressionVisitor
	{
		bool _rootLambda = true;
		ParameterExpression _parameter;

		public Dictionary<string, ElementSchema> ElementSchema = new Dictionary<string, ElementSchema>();
		public Dictionary<string, AttributeSchema> AttributeSchema = new Dictionary<string, AttributeSchema>();
		public int ArgLength = 0;

		public override Expression Visit(Expression node)
		{
			return base.Visit(node);
		}

		protected override Expression VisitLambda<T>(Expression<T> node)
		{
			if (_rootLambda)
			{
				_rootLambda = false;
				IEnumerable<ParameterExpression> newParameters = node.Parameters.Select(
					x =>
					{
						if (x.Type == typeof(INode))
						{
							_parameter = Expression.Parameter(typeof(ParserData), x.Name);
							return _parameter;
						}
						else
							return x;
					}).ToList();

				var visited = (LambdaExpression)base.VisitLambda(node);

				return Expression.Lambda(typeof(Func<ParserData, object>), Expression.Convert(visited.Body, typeof(object)), newParameters);
			}
			else
				return base.VisitLambda(node);
		}

		protected override Expression VisitMethodCall(MethodCallExpression node)
		{
			if (node.Object != null && node.Object.Type == typeof(INode))
			{
				switch (node.Method.Name)
				{
					case "Tag":
						return TagExpression();
				}

				var xmlNode = ((string)((ConstantExpression)node.Arguments[0]).Value);
				var argN = ArgLength++;

				switch (node.Method.Name)
				{
					case "Attribute":
						if (!AttributeSchema.ContainsKey(xmlNode))
						{
							AttributeSchema[xmlNode] = new AttributeSchema() { ParseToArgN = argN };
						}
						return GetExpression(typeof(string), argN);
				}

				if (!ElementSchema.ContainsKey(xmlNode))
				{
					ElementSchema[xmlNode] = new ElementSchema() { ParseToArgN = argN };
				}

				switch (node.Method.Name)
				{
					case "NullBool":
						ElementSchema[xmlNode].Parser = new NullBoolParser();
						return GetExpression(node.Type, argN);

					case "Int":
						ElementSchema[xmlNode].Parser = new IntParser();
						return GetExpression(node.Type, argN);

					case "NullInt":
						ElementSchema[xmlNode].Parser = new NullIntParser();
						return GetExpression(node.Type, argN);

					case "NullDecimal":
						ElementSchema[xmlNode].Parser = new NullDecimalParser();
						return GetExpression(node.Type, argN);

					case "NullDate":
						ElementSchema[xmlNode].Parser = new NullDateParser();
						return GetExpression(node.Type, argN);

					case "String":
						ElementSchema[xmlNode].Parser = new StringParser();
						return GetExpression(node.Type, argN);

					case "Custom":
						ElementSchema[xmlNode].Parser = GetCustomParser(node.Arguments[1]);
						return GetExpression(node.Type, argN);
					
					case "Type":
						ElementSchema[xmlNode].Parser = new TypeParser(GetLambda(node.Arguments[1]));
						return GetExpression(node.Type, argN);

					case "Array":
						ElementSchema[xmlNode].Parser = new ArrayParser(GetLambda(node.Arguments[1]));
						return GetExpression(node.Type, argN);

					default:
						throw new ApplicationException("unknown wapper function");
				}
			}
			return base.VisitMethodCall(node);
		}

		public Expression GetExpression(Type type, int argN)
		{
			var getMethod = typeof(ParserData).GetMethod("Get").MakeGenericMethod(type);
			return Expression.Call(_parameter, getMethod, Expression.Constant(argN));
		}

		public Expression TagExpression()
		{
			var getMethod = typeof(ParserData).GetMethod("Tag");
			return Expression.Call(_parameter, getMethod);
		}

		public static IXmlParser GetCustomParser(object parserRaw)
		{
			var lambda = Expression.Lambda((Expression)parserRaw).Compile();
			var parser = new CustomParser((Func<Func<XmlReader, object>>)lambda);
			return parser;
		}

		LambdaExpression GetLambda(Expression expression)
		{
			if (expression is LambdaExpression)
				return (LambdaExpression)expression;
			else if (expression.NodeType == ExpressionType.Quote)
				return (LambdaExpression)((UnaryExpression)expression).Operand;
			else
				throw new Exception("Malformed expression tree");
		}
	}

	public interface INode
	{
		bool? NullBool(string name);
		int Int(string name);
		int? NullInt(string name);
		decimal? NullDecimal(string name);
		string String(string name);
		DateTime? NullDate(string name);
		T Type<T>(string name, Expression<Func<INode, T>> func);
		T[] Array<T>(string name, Expression<Func<INode, T>> func);
		T Custom<T>(string name, Func<XmlReader, T> parser);
		string Tag();
		string Attribute(string name);
	}

	class ParserData
	{
		public object[] Data { get; private set; }
		public string TagName;

		public ParserData(int dataLength)
		{
			Data = new object[dataLength];
		}

		public string Tag()
		{
			return TagName;
		}

		public T Get<T>(int argnN)
		{
			if (Data[argnN] == null)
				return default(T);
			return (T)Data[argnN];
		}
	}
}
