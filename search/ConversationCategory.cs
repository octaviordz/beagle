using Gtk;
using Gdk;
using System;
using System.Collections;

namespace Search {

	public class ConversationCategory : Category {

		Gtk.SizeGroup col1, col2, col3;

		public ConversationCategory (string name, string moreString, int few, int many) : base (name, moreString, few, many, 1)
		{
			col1 = new Gtk.SizeGroup (Gtk.SizeGroupMode.Horizontal);
			col2 = new Gtk.SizeGroup (Gtk.SizeGroupMode.Horizontal);
			col3 = new Gtk.SizeGroup (Gtk.SizeGroupMode.Horizontal);
		}

		protected override void OnAdded (Gtk.Widget widget)
		{
			base.OnAdded (widget);

			Tiles.MailMessage mtile = widget as Tiles.MailMessage;
			if (mtile != null) {
				col1.AddWidget (mtile.SubjectLabel);
				col2.AddWidget (mtile.FromLabel);
				col3.AddWidget (mtile.DateLabel);
				return;
			}

			Tiles.IMLog imtile = widget as Tiles.IMLog;
			if (imtile != null) {
				col1.AddWidget (imtile.SubjectLabel);
				col2.AddWidget (imtile.FromLabel);
				col3.AddWidget (imtile.DateLabel);
				return;
			}
		}

		protected override void OnSizeRequested (ref Requisition req)
		{
			Requisition headerReq, tileReq;

			headerReq = header.SizeRequest ();

			tileReq.Width = tileReq.Height = 0;
			foreach (Widget w in AllTiles) {
				tileReq = w.SizeRequest ();
				req.Width = Math.Max (req.Width, tileReq.Width);
				req.Height = Math.Max (req.Height, tileReq.Height);
			}

			req.Height = headerReq.Height + PageSize * tileReq.Height;
			req.Width = Math.Max (headerReq.Width + headerReq.Height,
					      tileReq.Width + 2 * headerReq.Height);

			req.Width += (int)(2 * BorderWidth);
			req.Height += (int)(2 * BorderWidth);
		}

		protected override void OnSizeAllocated (Rectangle allocation)
		{
			Requisition headerReq;
			Rectangle childAlloc;

			base.OnSizeAllocated (allocation);

			headerReq = header.ChildRequisition;

			childAlloc.X = allocation.X + (int)BorderWidth + headerReq.Height;
			childAlloc.Width = allocation.Width - (int)BorderWidth - headerReq.Height;
			childAlloc.Y = allocation.Y + (int)BorderWidth;
			childAlloc.Height = headerReq.Height;
			header.Allocation = childAlloc;

			childAlloc.X += headerReq.Height;
			childAlloc.Width -= headerReq.Height;
			foreach (Widget w in VisibleTiles) {
				childAlloc.Y += childAlloc.Height;
				childAlloc.Height = w.ChildRequisition.Height;
				w.Allocation = childAlloc;
			}
		}
	}
}