using System.Collections.Generic;
using System.Linq;

namespace XmlStreamProcessor.Test
{
	class Sample
	{
		public int SomeInt;
		public string SomeString;
		public List<Subclass> Subclasses;
		public string Tag;

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}
			var other = (Sample)obj;

			return SomeInt == other.SomeInt
			       && SomeString == other.SomeString
			       && Tag == other.Tag
			       && Subclasses.SequenceEqual(other.Subclasses);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}
}