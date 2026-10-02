using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CHMBP1
{
    public partial class Form1 : Form
    {
        private const int CellSize = 10;

        private static readonly Color HealthyColor = ColorTranslator.FromHtml("#A5D6A7");
        private static readonly Color InfectedColor = ColorTranslator.FromHtml("#E53935");
        private static readonly Color ImmuneColor = ColorTranslator.FromHtml("#64B5F6");
        private static readonly Color DeadColor = ColorTranslator.FromHtml("#424242");

        private readonly Random random = new Random();
        private readonly Timer timer1 = new Timer();

        // Кисти создаются один раз: раньше на каждую клетку каждого кадра создавалась новая кисть.
        private readonly SolidBrush healthyBrush = new SolidBrush(HealthyColor);
        private readonly SolidBrush infectedBrush = new SolidBrush(InfectedColor);
        private readonly SolidBrush immuneBrush = new SolidBrush(ImmuneColor);
        private readonly SolidBrush deadBrush = new SolidBrush(DeadColor);

        private Simulation simulation;
        private int durationDays;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;
            pictureBox1.BackColor = Color.LightGray;
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;

            timer1.Interval = 300; // скорость: 1 день = 300 мс
            timer1.Tick += Timer1_Tick;

            // По умолчанию радиус 1 (четыре соседа). Раньше поле было пустым, галочки соседей
            // не стояли, и вирус не мог передаваться, а первый же шаг падал на int.Parse("").
            textBox10.Text = "1";

            // Значения в полях уже заполнены, поэтому применяем их сразу:
            // раньше до нажатия «Применить» вирус работал с нулевыми параметрами.
            ApplySettings(false);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer1.Stop();
            timer1.Dispose();
            DisposeImage();
            healthyBrush.Dispose();
            infectedBrush.Dispose();
            immuneBrush.Dispose();
            deadBrush.Dispose();
            base.OnFormClosed(e);
        }

        // Настройки

        private void button1_Click(object sender, EventArgs e)
        {
            ApplySettings(true);
        }

        /// <summary>Читает параметры из полей и начинает новый эксперимент. true, если всё корректно.</summary>
        private bool ApplySettings(bool showErrors)
        {
            VirusParameters parameters;
            string error = ReadParameters(out parameters);
            if (error != null)
            {
                if (showErrors)
                    MessageBox.Show(error, "Некорректные параметры", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Пресеты радиуса (1 или 2) выставляют галочки соседей.
            textBox10_TextChanged(null, null);

            timer1.Stop();
            durationDays = parameters.DurationDays;
            simulation = new Simulation(parameters, ReadNeighbors(), random);
            Redraw();
            return true;
        }

        private string ReadParameters(out VirusParameters parameters)
        {
            parameters = new VirusParameters();
            string error;

            if ((error = ReadInt(textBox5, "Длительность эксперимента", out parameters.DurationDays)) != null) return error;
            if ((error = ReadInt(textBox6, "Инкубационный период", out parameters.IncubationDays)) != null) return error;
            if ((error = ReadInt(textBox4, "Вероятность заражения", out parameters.InfectionChance)) != null) return error;
            if ((error = ReadInt(textBox3, "Вероятность смерти", out parameters.Lethality)) != null) return error;
            if ((error = ReadInt(textBox2, "Период иммунитета", out parameters.ImmunityDays)) != null) return error;
            if ((error = ReadInt(textBox1, "Начальный % иммунитета", out parameters.InitialImmunePercent)) != null) return error;
            if ((error = ReadInt(textBox7, "Начальный % больных", out parameters.InitialInfectedPercent)) != null) return error;
            if ((error = ReadInt(textBox8, "Исчезновение больных с границ", out parameters.BorderDisappearPercent)) != null) return error;
            if ((error = ReadInt(textBox9, "Длительность болезни", out parameters.IllnessDays)) != null) return error;

            return parameters.Validate();
        }

        private static string ReadInt(TextBox box, string fieldName, out int value)
        {
            if (!int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return "Поле «" + fieldName + "» должно быть целым числом.";
            return null;
        }

        /// <summary>Радиус 1: четыре соседа по сторонам, радиус 2: все двенадцать клеток.</summary>
        private void textBox10_TextChanged(object sender, EventArgs e)
        {
            int radius;
            if (!int.TryParse(textBox10.Text.Trim(), out radius))
                return;

            if (radius == 1)
            {
                SetNeighborChecks(checkBox7, checkBox5, checkBox6, checkBox9);
            }
            else if (radius == 2)
            {
                SetNeighborChecks(
                    checkBox1, checkBox2, checkBox3, checkBox4, checkBox5, checkBox6,
                    checkBox7, checkBox8, checkBox9, checkBox10, checkBox11, checkBox12);
            }
        }

        /// <summary>Отмечает только перечисленные галочки, остальные снимает.</summary>
        private void SetNeighborChecks(params CheckBox[] checkedBoxes)
        {
            List<CheckBox> selected = new List<CheckBox>(checkedBoxes);
            foreach (KeyValuePair<Offset, CheckBox> entry in NeighborMap())
                entry.Value.Checked = selected.Contains(entry.Value);
        }

        /// <summary>Какая галочка отвечает за какого соседа (строка, столбец относительно клетки).</summary>
        private Dictionary<Offset, CheckBox> NeighborMap()
        {
            return new Dictionary<Offset, CheckBox>
            {
                { new Offset(-2, 0), checkBox4 },
                { new Offset(-1, -1), checkBox12 },
                { new Offset(-1, 0), checkBox7 },
                { new Offset(-1, +1), checkBox8 },
                { new Offset(0, -2), checkBox3 },
                { new Offset(0, -1), checkBox9 },
                { new Offset(0, +1), checkBox6 },
                { new Offset(0, +2), checkBox1 },
                { new Offset(+1, -1), checkBox10 },
                { new Offset(+1, 0), checkBox5 },
                { new Offset(+1, +1), checkBox11 },
                { new Offset(+2, 0), checkBox2 }
            };
        }

        private List<Offset> ReadNeighbors()
        {
            List<Offset> offsets = new List<Offset>();
            foreach (KeyValuePair<Offset, CheckBox> entry in NeighborMap())
                if (entry.Value.Checked)
                    offsets.Add(entry.Key);
            return offsets;
        }

        // Эксперимент

        private void button2_Click(object sender, EventArgs e)
        {
            NextDay();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (simulation == null || IsFinished())
                return;
            timer1.Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            NextDay();
        }

        private bool IsFinished()
        {
            return simulation.Day >= durationDays || simulation.IsOver;
        }

        private void NextDay()
        {
            if (simulation == null || IsFinished())
            {
                timer1.Stop();
                return;
            }

            simulation.Step();
            Redraw();

            // Эксперимент закончился по времени или больных не осталось.
            if (IsFinished())
                timer1.Stop();
        }

        // Отрисовка

        private void Redraw()
        {
            DrawField();
            UpdateStats();
        }

        private void UpdateStats()
        {
            string title = string.Format("День {0} из {1}", simulation.Day, durationDays);
            if (simulation.IsOver)
                title += ": эпидемия закончилась";
            else if (simulation.Day >= durationDays)
                title += ": эксперимент завершён";

            labelStats.Text = title + Environment.NewLine + string.Format(
                "Здоровых: {0}   Больных: {1}   Иммунных: {2}   Умерших: {3}",
                simulation.Count(PersonState.Healthy),
                simulation.Count(PersonState.Infected),
                simulation.Count(PersonState.Immune),
                simulation.Count(PersonState.Dead));
        }

        private void DrawField()
        {
            Bitmap bmp = new Bitmap(Simulation.Width * CellSize, Simulation.Height * CellSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int i = 0; i < Simulation.Height; i++)
                {
                    for (int j = 0; j < Simulation.Width; j++)
                    {
                        g.FillRectangle(BrushFor(simulation.GetState(i, j)), j * CellSize, i * CellSize, CellSize - 1, CellSize - 1);
                    }
                }
            }

            DisposeImage();
            pictureBox1.Image = bmp;
        }

        private Brush BrushFor(PersonState state)
        {
            switch (state)
            {
                case PersonState.Infected: return infectedBrush;
                case PersonState.Immune: return immuneBrush;
                case PersonState.Dead: return deadBrush;
                default: return healthyBrush;
            }
        }

        /// <summary>Освобождает предыдущий кадр: раньше каждый кадр оставался в памяти.</summary>
        private void DisposeImage()
        {
            Image old = pictureBox1.Image;
            pictureBox1.Image = null;
            if (old != null)
                old.Dispose();
        }
    }
}
