using System;

namespace Nanami
{
	internal static class Extensions
	{
		public static T NotNull<T>(this T obj) where T : class
		{
			if (obj == null)
			{
				throw new ArgumentNullException(nameof(obj));
			}
			return obj;
		}
	}
}
