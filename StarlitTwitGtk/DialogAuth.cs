using System;

namespace StarlitTwitGtk
{
	public partial class DialogAuth : Gtk.Dialog
	{
		public DialogAuth ()
		{
			this.Build ();
		}

        public string PIN {
            get { return entry1.Text; }
        }

        protected void OnButtonCancelClicked (object sender, System.EventArgs e)
        {
            this.Respond(Gtk.ResponseType.Cancel);
        }

        protected void OnButtonOkClicked (object sender, System.EventArgs e)
        {
            this.Respond(Gtk.ResponseType.Ok);
        }
	}
}

