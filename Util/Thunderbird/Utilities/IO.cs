//
// IO.cs: Basic IO
//
// Copyright (C) 2007 Pierre Östlund
//

//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using System;
using System.IO;

namespace Beagle.Util.Thunderbird.Utilities {
	
	public static class IO {
		
		// Move to Mork.Utilities
		/* public static bool IsFullyIndexable (string mailbox_file)
		{
			throw new NotImplementedException ();
		} */
		
		public static long GetFileSize (string filename)
		{
			FileStream stream = new FileStream (filename, FileMode.Open);
			long length = stream.Length;
			stream.Close ();

			return length;
		}
		
		public static bool IsEmpty (string filename)
		{
			if (GetFileSize (filename) == 0)
				return true;
			
			return false;
		}
	}
}