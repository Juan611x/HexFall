using NUnit.Framework;

namespace HexFall.UI.Tests
{
    public class InputRulesTests
    {
        [TestCase("Ash", NameCheck.Valid)]
        [TestCase("Ashen_Mage_042", NameCheck.Valid)]
        [TestCase("abcdefghijklmnop", NameCheck.Valid)]
        [TestCase("", NameCheck.Empty)]
        [TestCase(null, NameCheck.Empty)]
        [TestCase("ab", NameCheck.Invalid)]
        [TestCase("abcdefghijklmnopq", NameCheck.Invalid)]
        [TestCase("bad name", NameCheck.Invalid)]
        [TestCase("wizard!", NameCheck.Invalid)]
        [TestCase("magé_01", NameCheck.Invalid)]
        public void WizardNameRuleEvaluates(string name, NameCheck expected)
        {
            Assert.AreEqual(expected, WizardNameRule.Evaluate(name));
            Assert.AreEqual(expected == NameCheck.Valid, WizardNameRule.IsValid(name));
        }

        [TestCase("wizard@example.com", true)]
        [TestCase("a.b+c@sub.example.org", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("wizard", false)]
        [TestCase("wizard@", false)]
        [TestCase("@example.com", false)]
        [TestCase("wizard@example", false)]
        [TestCase("wiz ard@example.com", false)]
        [TestCase("a@@example.com", false)]
        [TestCase("a@example.", false)]
        public void EmailRuleEvaluates(string email, bool expected)
        {
            Assert.AreEqual(expected, EmailRule.IsValid(email));
        }

        [Test]
        public void PasswordPolicyReportsEachRule()
        {
            var c = PasswordPolicy.Evaluate("Arena#2026", "Arena#2026");
            Assert.IsTrue(c.MinLength && c.HasUpper && c.HasLower && c.HasDigit && c.HasSymbol && c.Matches);
            Assert.IsTrue(c.AllMet);
        }

        [TestCase("Ab#1xyz", "Ab#1xyz", false, true, true, true, true, true)]
        [TestCase("arena#2026", "arena#2026", true, false, true, true, true, true)]
        [TestCase("ARENA#2026", "ARENA#2026", true, true, false, true, true, true)]
        [TestCase("Arena#abcd", "Arena#abcd", true, true, true, false, true, true)]
        [TestCase("Arena2026", "Arena2026", true, true, true, true, false, true)]
        [TestCase("Arena#2026", "Arena#2027", true, true, true, true, true, false)]
        public void PasswordPolicySingleRuleFailures(string pw, string confirm, bool len, bool up, bool low, bool dig, bool sym, bool match)
        {
            var c = PasswordPolicy.Evaluate(pw, confirm);
            Assert.AreEqual((len, up, low, dig, sym, match), (c.MinLength, c.HasUpper, c.HasLower, c.HasDigit, c.HasSymbol, c.Matches));
            Assert.IsFalse(c.AllMet);
        }

        [Test]
        public void PasswordPolicyEmptyPasswordMeetsNothingNotEvenMatch()
        {
            var c = PasswordPolicy.Evaluate("", "");
            Assert.IsFalse(c.Matches);
            Assert.IsFalse(c.MinLength);
            Assert.IsFalse(c.AllMet);
            Assert.IsFalse(PasswordPolicy.Evaluate(null, null).Matches);
        }
    }

    public class VerificationCodeInputTests
    {
        private VerificationCodeInput _input;

        [SetUp]
        public void SetUp() => _input = new VerificationCodeInput();

        [Test]
        public void StartsEmpty()
        {
            Assert.AreEqual("", _input.Code);
            Assert.IsFalse(_input.IsComplete);
            Assert.IsNull(_input.DigitAt(0));
        }

        [Test]
        public void TypingADigitStoresItAndAdvances()
        {
            Assert.AreEqual(1, _input.Type(0, "4"));
            Assert.AreEqual('4', _input.DigitAt(0));
        }

        [Test]
        public void TypingInLastBoxKeepsFocusThere()
        {
            Assert.AreEqual(5, _input.Type(5, "9"));
        }

        [Test]
        public void NonDigitsAreIgnored()
        {
            Assert.AreEqual(2, _input.Type(2, "x"));
            Assert.IsNull(_input.DigitAt(2));
        }

        [Test]
        public void EmptyTextClearsTheBox()
        {
            _input.Type(1, "7");
            Assert.AreEqual(1, _input.Type(1, ""));
            Assert.IsNull(_input.DigitAt(1));
        }

        [Test]
        public void TypingOverAFilledBoxSpillsIntoTheNext()
        {
            _input.Type(0, "1");
            Assert.AreEqual(2, _input.Type(0, "12"));
            Assert.AreEqual('1', _input.DigitAt(0));
            Assert.AreEqual('2', _input.DigitAt(1));
        }

        [Test]
        public void PasteOfSixDigitsFillsAllAndFocusesLast()
        {
            Assert.AreEqual(5, _input.Type(0, "123456"));
            Assert.AreEqual("123456", _input.Code);
            Assert.IsTrue(_input.IsComplete);
        }

        [Test]
        public void PasteStripsNonDigitsAndTruncates()
        {
            _input.Type(0, "12-34 56 78");
            Assert.AreEqual("123456", _input.Code);
        }

        [Test]
        public void BackspaceOnFilledBoxClearsItAndStays()
        {
            _input.Type(0, "1");
            _input.Type(1, "2");
            Assert.AreEqual(1, _input.Backspace(1));
            Assert.IsNull(_input.DigitAt(1));
            Assert.AreEqual('1', _input.DigitAt(0));
        }

        [Test]
        public void BackspaceOnEmptyBoxMovesBackAndClearsPrevious()
        {
            _input.Type(0, "1");
            Assert.AreEqual(0, _input.Backspace(1));
            Assert.IsNull(_input.DigitAt(0));
        }

        [Test]
        public void BackspaceInFirstEmptyBoxStaysAtZero()
        {
            Assert.AreEqual(0, _input.Backspace(0));
        }

        [Test]
        public void ClearResetsEverything()
        {
            _input.Type(0, "123456");
            _input.Clear();
            Assert.AreEqual("", _input.Code);
            Assert.IsFalse(_input.IsComplete);
        }
    }
}
