using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergeControl
{
    internal static class Enumerations
    {
		internal enum LogType : byte
        {
			None = 0,
			Info,
			Warning,
			Error,
			Fatal,
			Debug
        }

		internal enum MergeType : byte
		{
			/// <summary>The tile merges normally</summary>
			Default = 0,

			// Ranges 1 - 249 are reserved for manual sprites

			/// <summary>Used to tell servers to refresh tile data.</summary>
			Handshake = byte.MaxValue - 6,

			/// <summary>Actuate tile - used for packets exclusively.</summary>
			Actuation = byte.MaxValue - 5,

			/// <summary>No merging - act as if always surrounded by air.</summary>
			Alone = byte.MaxValue - 4,

			/// <summary>Grouping 1 - Only merge with other group 1 tiles</summary>
			Group1 = byte.MaxValue - 3,
			/// <summary>Grouping 2 - Only merge with other group 2 tiles</summary>
			Group2 = byte.MaxValue - 2,
			/// <summary>Grouping 3 - Only merge with other group 3 tiles</summary>
			Group3 = byte.MaxValue - 1,
			/// <summary>Grouping 4 - Only merge with other group 4 tiles</summary>
			Group4 = byte.MaxValue,
		}
	}
}
