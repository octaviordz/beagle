//
// TileDocs.cs
//
// Copyright (C) 2004 Novell, Inc.
//

//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using Mono.Unix;

using BU = Beagle.Util;

namespace Beagle.Tile {
	
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/vnd.sun.xml.writer")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType = "application/vnd.sun.xml.writer.template")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/msword")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/vnd.ms-word")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/x-msword")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/pdf")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/x-abiword")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/rtf")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/x-chm")]
		
	// OO 2.0 (writer) formats
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType="application/vnd.oasis.opendocument.text")]
	[HitFlavor (Name="Docs", Rank=400, Emblem="", Color="#f5f5fe",
		    Type="File", MimeType = "application/vnd.oasis.opendocument.text.template")]
		
		public class TileDocs : TileFile {
			public TileDocs (Hit _hit) : base (_hit, "template-docs.html")
			{
			}
			
			protected override void PopulateTemplate ()
			{
				StringBuilder strPagesAndWords = new StringBuilder ();

				base.PopulateTemplate ();
				if (Hit ["dc:title"] != null && Hit ["dc:title"].Trim () != "")
					Template ["Title"] = Hit ["dc:title"];

                                if (Hit ["fixme:page-count"] != null)
					strPagesAndWords.Append (Catalog.GetString ("Pages:") + " " + Hit ["fixme:page-count"]);
				//Template ["PagesAndWords"] = Hit ["fixme:page-count"];

                                if (Hit ["fixme:word-count"] != null)
					strPagesAndWords.Append (" - " + Catalog.GetString ("Words:") + " " + Hit ["fixme:word-count"]);
				//Template ["Words"] = Hit ["fixme:word-count"];
				Template["PagesAndWords"] = strPagesAndWords.ToString();
			}
		}
}
