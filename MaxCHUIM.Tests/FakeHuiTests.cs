using System;
using Xunit;

namespace MaxCHUIM.Tests
{
    public class FakeHuiTests
    {
        [Fact]
        public void RunFakeHuiCheck()
        {
            UtilityChecker.CheckFakeHUIs();
        }
    }
}
