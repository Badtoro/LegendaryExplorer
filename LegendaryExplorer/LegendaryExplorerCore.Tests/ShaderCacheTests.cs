using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ShaderCacheTests
    {
        [TestMethod]
        public void TestGlobalShaderCacheReserialization()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var binsPath = GlobalTest.GetGlobalShaderCachesDirectory();
            var bins = Directory.GetFiles(binsPath, "*.bin", SearchOption.AllDirectories);
            foreach (var shaderCacheFile in bins)
            {
                MEGame game = Enum.Parse<MEGame>(Path.GetFileNameWithoutExtension(shaderCacheFile));
                using var input = new MemoryStream(File.ReadAllBytes(shaderCacheFile));
                var inCache = ShaderCache.ReadGlobalShaderCache(input, game);
                var outS = new MemoryStream();
                PackagelessSerializingContainer container = new(outS, null)
                {
                    Game = game
                };
                inCache.WriteTo(container);
#if DEBUG
                if (outS.Length != input.Length)
                {
                    DebugTools.DebugUtilities.CompareByteArrays(input.ToArray(), outS.ToArray());
                }
#endif

                Assert.IsTrue(outS.ToArray().SequenceEqual(input.ToArray()), $"Serialization of {game} GlobalShaderCache failed - data size or contents did not match. Source length: {input.Length}, Output length: {outS.Length}");
            }
        }
    }
}