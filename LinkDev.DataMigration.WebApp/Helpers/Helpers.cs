using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace LinkDev.DataMigration.WebApp.Helpers
{
    public static class Helpers
    {
		internal static string Compress(object obj)
		{
			using (var compressedStream = new MemoryStream())
			{
				using (var uncompressedStream = new MemoryStream(Serialise(obj)))
				{
					using (var compressorStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
					{
						uncompressedStream.CopyTo(compressorStream);
					}
				}

				return Convert.ToBase64String(compressedStream.ToArray());
			}
		}

	    public static T Decompress<T>(string rawData)
	    {
		    using (var decompressedStream = new MemoryStream())
		    {
				using (var compressedStream = new MemoryStream(Convert.FromBase64String(rawData)))
				{
					using (var decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
					{
						decompressorStream.CopyTo(decompressedStream);
						return Deserialise<T>(decompressedStream.ToArray());
					}
				}
		    }
	    }

		public static byte[] Serialise(object obj)
	    {
			using (var memoryStream = new MemoryStream())
			{
				new DataContractSerializer(obj.GetType()).WriteObject(memoryStream, obj);
				return memoryStream.ToArray();
			}
		}

	    public static T Deserialise<T>(byte[] bytes)
	    {
		    using (var stream = new MemoryStream(bytes))
		    {
			    var serializer = new DataContractSerializer(typeof(T));
			    return (T)serializer.ReadObject(stream);
		    }
		}
	}
}
