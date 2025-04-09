using System.ComponentModel;
using WinTransform.Helpers;

namespace WinTransform;

[DesignerCategory("")]
class MainForm : Form
{
    private ComboBox _targets;

    public MainForm()
    {
        var panel = new FlowLayoutPanel().AutoSize();
        panel.FlowDirection = FlowDirection.TopDown;
        panel.Controls.Add(TargetsPanel());
        panel.Controls.Add(Button());
        Controls.Add(panel);
        this.AutoSize();
        Height = 0;
        return;

        FlowLayoutPanel TargetsPanel()
        {
            var targetsPanel = new FlowLayoutPanel().AutoSize();
            targetsPanel.FlowDirection = FlowDirection.LeftToRight;
            var label = new Label().AutoSize();
            label.Text = "Target";
            targetsPanel.Controls.Add(label);
            targetsPanel.Controls.Add(Targets());
            return targetsPanel;
        }
        ComboBox Targets()
        {
            _targets = new ComboBox().AutoSize();
            _targets.Width = 300;
            UpdateTargets().NoAwait();
            return _targets;

            async Task UpdateTargets()
            {
                while (true)
                {
                    _targets.Items.Clear();
                    _targets.Items.AddRange(Capturable.GetAll());
                    await Task.Delay(1000);
                }
            }
        }
        Button Button()
        {
            var button = new Button().AutoSize();
            button.Text = "Transform";
            button.Click += (s, e) =>
            {
                if (Capturable.GetAll().SingleOrDefault(c => c.Name == _targets.Text) is { } capturable)
                {
                    new RenderForm(new ImageProvider(capturable.GetItem())).Show();
                }
            };
            return button;
        }
    }

}