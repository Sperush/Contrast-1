using System;

namespace Contrast.Core
{
    /// <summary>
    /// Global Singleton configuration containing the master <see cref="enableTest"/> flag.
    /// When <c>enableTest == true</c>, test/debug shortcuts (such as T free-move/no-clip,
    /// 1/2/3 direct color change, Up/Down step color chunk, and 1/2/3 menu quick-select)
    /// work normally. When <c>enableTest == false</c>, all test shortcuts are disabled.
    /// </summary>
    [Serializable]
    public sealed class Config
    {
        private static Config instance;

        public static Config Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Config();
                }
                return instance;
            }
        }

        /// <summary>
        /// Master flag to enable or disable all test/debug shortcuts across the game.
        /// Set to <c>true</c> for testing/debugging; set to <c>false</c> to disable all test keys.
        /// </summary>
        public bool enableTest = true;
    }
}
