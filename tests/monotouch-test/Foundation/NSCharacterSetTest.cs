﻿using System;
using System.Drawing;
#if XAMCORE_2_0
using Foundation;
using UIKit;
#else
using MonoTouch.Foundation;
using MonoTouch.UIKit;
#endif
using NUnit.Framework;

#if XAMCORE_2_0
using RectangleF=CoreGraphics.CGRect;
using SizeF=CoreGraphics.CGSize;
using PointF=CoreGraphics.CGPoint;
#else
using nfloat=global::System.Single;
using nint=global::System.Int32;
using nuint=global::System.UInt32;
#endif

namespace MonoTouchFixtures.Foundation
{
	[TestFixture]
	[Preserve (AllMembers = true)]
	public class NSCharacterSetTest
	{
		static void RequiresIos8 ()
		{
			if (!TestRuntime.CheckSystemAndSDKVersion (8, 0))
				Assert.Inconclusive ("Requires iOS8+");
		}

		[Test]
		public void NSMutableCharacterSet_TestStaticSets ()
		{
			RequiresIos8 ();

			TestSet (NSMutableCharacterSet.Alphanumerics, "Alphanumerics", 'a');
			TestSet (NSMutableCharacterSet.Capitalized, "Capitalized", '\u01C5');
			TestSet (NSMutableCharacterSet.Controls, "Controls", '\u0000');
			TestSet (NSMutableCharacterSet.DecimalDigits, "DecimalDigits", '1');
			TestSet (NSMutableCharacterSet.Decomposables, "Decomposables", '\u00e9');
			TestSet (NSMutableCharacterSet.Illegals, "Illegals", '\uFFFE');
			TestSet (NSMutableCharacterSet.Letters, "Letters", 'a');
			TestSet (NSMutableCharacterSet.LowercaseLetters, "LowercaseLetters", 'a');
			TestSet (NSMutableCharacterSet.Newlines, "Newlines", '\n');
			TestSet (NSMutableCharacterSet.Marks, "Marks", '\u20DD');
			TestSet (NSMutableCharacterSet.Punctuation, "Punctuation", '.');
			TestSet (NSMutableCharacterSet.Symbols, "Symbols", '~');
			TestSet (NSMutableCharacterSet.UppercaseLetters, "UppercaseLetters", 'A');
			TestSet (NSMutableCharacterSet.WhitespaceAndNewlines, "WhitespaceAndNewlines", ' ');
			TestSet (NSMutableCharacterSet.Whitespaces, "Whitespaces", ' ');
		}

		void TestSet (NSCharacterSet s, string setName, char characterThatShouldBeInSet)
		{
			RequiresIos8 ();

			Assert.IsNotNull (s, setName + " was null");
			Assert.IsTrue (s.Contains (characterThatShouldBeInSet), setName + " did not contain: " + characterThatShouldBeInSet);
		}
	}
}

