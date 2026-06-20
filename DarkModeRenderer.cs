namespace AutoTotal
{
    public class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuBorder => Color.FromArgb(65, 65, 65);
        public override Color MenuItemBorder => Color.FromArgb(65, 65, 65);
        public override Color MenuItemSelected => Color.FromArgb(65, 65, 65);
        public override Color MenuStripGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color MenuStripGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(65, 65, 65);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(65, 65, 65);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(65, 65, 65);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(65, 65, 65);
    }

    public class DarkModeRenderer : ToolStripProfessionalRenderer
    {
        public DarkModeRenderer() : base(new DarkModeColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (var brush = new SolidBrush(Color.FromArgb(65, 65, 65)))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
                }
                using (var pen = new Pen(Color.FromArgb(65, 65, 65)))
                {
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1));
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            using (var pen = new Pen(Color.FromArgb(65, 65, 65)))
            {
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
    }
}
