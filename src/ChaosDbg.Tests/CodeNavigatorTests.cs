﻿using System;
using ChaosDbg.Disasm;
using ChaosDbg.Engine;
using ChaosDbg.Metadata;
using ChaosDbg.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class CodeNavigatorTests
    {
        #region HeaderSegment

        [TestMethod]
        public void CodeNavigation_HeaderSegment_StepDown()
        {
            Test(0, v =>
            {
                v.StepDown(1).StepDown(2).StepDown(3);
            });
        }

        [TestMethod]
        public void CodeNavigation_HeaderSegment_StepDown_StepUp()
        {
            Test(0, v =>
            {
                v.StepDown(1).StepDown(2).StepUp(1);
            });
        }

        [TestMethod]
        public void CodeNavigation_HeaderSegment_StepDown_StepUp_ToSegmentStart()
        {
            Test(0, v =>
            {
                v.StepDown(1).StepUp(0);
            });
        }

        [TestMethod]
        public void CodeNavigation_HeaderSegment_StepUp_BeyondSegmentStart()
        {
            Test(0, v =>
            {
                v.StepUp(0);
            });
        }

        [TestMethod]
        public void CodeNavigation_HeaderSegment_SingleScrollDown_GetLines()
        {
            Test(0, v =>
            {
                v.StepDown(1).StepDown(2).StepDown(3).GetLines(2, 30, "2", "29");
            });
        }

        #endregion
        #region CodeSegment

        [TestMethod]
        public void CodeNavigation_CodeSegment_StepDown()
        {
            Test(1, v =>
            {
                v.StepDown(0x1002).StepDown(0x1004).StepDown(0x1006);
            });
        }

        [TestMethod]
        public void CodeNavigation_CodeSegment_StepDown_StepUp()
        {
            Test(1, v =>
            {
                v.StepDown(0x1002).StepDown(0x1004).StepUp(0x1002);
            });
        }

        [TestMethod]
        public void CodeNavigation_CodeSegment_StepDown_StepUp_ToSegmentStart()
        {
            Test(1, v =>
            {
                v.StepDown(0x1002).StepUp(0x1000);
            });
        }

        [TestMethod]
        public void CodeNavigation_CodeSegment_StepUp_BeyondSegmentStart()
        {
            Test(1, v =>
            {
                v.StepUp(0x1000);
            });
        }

        [TestMethod]
        public void CodeNavigation_CodeSegment_SingleScrollDown_GetLines()
        {
            Test(1, v =>
            {
                v.StepDown(0x1002).StepDown(0x1004).StepDown(0x1006).GetLines(0x1005, 0x1021, "4b281004", "4b281041");
            });
        }

        #endregion
        #region Across Sections

        [TestMethod]
        public void CodeNavigation_CrossSection_HeaderToCode_SingleStep()
        {
            Test(v =>
            {
                v.StepDown(0xFFF, 0xFFF).StepDown(1, 0x1000);
            });
        }

        [TestMethod]
        public void CodeNavigation_CrossSection_CodeToHeader_SingleStep()
        {
            Test(v =>
            {
                v.StepDown(0x1000, 0x1000).StepUp(1, 0xFFF);
            });
        }

        [TestMethod]
        public void CodeNavigation_CrossSection_HeaderToCode_Seek()
        {
            Test(v =>
            {
                v.SeekVertical(0x2000, 0x1ffe);
            });
        }

        [TestMethod]
        public void CodeNavigation_CrossSection_CodeToHeader_Seek()
        {
            Test(v =>
            {
                v.SeekVertical(0x2000, 0x1ffe).SeekVertical(20, 20);
            });
        }

        [TestMethod]
        public void CodeNavigation_CrossSection_SeekFromCodeToHeader_PageDownToCode()
        {
            //Upon going back up beyond the start of the code segment, its last address should be set to 0x1000 again
            Test(v =>
            {
                v.SeekVertical(0x1000, 0x1000).StepDown(100, 4296);
                v.SeekVertical(0x300, 0x300).StepDown(0x1000, 0x16af);
            });
        }

        [TestMethod]
        public void CodeNavigation_CrossSection_GetLines()
        {
            Test(v =>
            {
                Action<ITextLine[]> verifyLines = l =>
                {
                    Assert.AreEqual("4090 header", l[0].ToString());
                    Assert.AreEqual("4095 header", l[5].ToString());
                    Assert.AreEqual("4b281000 1800            sbb     byte ptr [eax],al", l[6].ToString());
                    Assert.AreEqual("4b28102d 55              push    ebp", l[27].ToString());
                };

                v.StepDown(4091, 4091).GetLines(4090, 4118, verifyLines);
            });
        }

        #endregion

        private void Test(int segment, Action<MemoryTextSegmentVerifier> verify)
        {
            var nav = CreateNavigator().Segments[segment];

            verify(nav.Verify());
        }

        private void Test(Action<CodeNavigatorVerifier> verify)
        {
            var nav = CreateNavigator();

            verify(nav.Verify());
        }

        private CodeNavigator CreateNavigator()
        {
            var path = "C:\\windows\\SysWOW64\\ntdll.dll";

            var serviceProvider = BuildServiceProvider();

            var peProvider = serviceProvider.GetService<IPEFileProvider>();
            var disasmProvider = serviceProvider.GetService<INativeDisassemblerProvider>();
            var nativeDisassembler = disasmProvider.CreateDisassembler(path);

            var pe = peProvider.ReadFile(path);

            var nav = new CodeNavigator(pe, nativeDisassembler);

            return nav;
        }

        private IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection
            {
                { typeof(IPEFileProvider), typeof(PEFileProvider) },
                { typeof(INativeDisassemblerProvider), typeof(NativeDisassemblerProvider) }
            };

            return services.Build();
        }
    }
}