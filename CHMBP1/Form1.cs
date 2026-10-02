using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CHMBP1
{
    public partial class Form1 : Form
    {
        private const int HEIGHT = 50;
        private const int WIDTH = 51;

        private int duration = 2000;
        private int startImmunityPercent = 1;
        private int startIllPercent = 10;
        private int illnessDeathRate = 100;

        private readonly Person[,] persons = new Person[HEIGHT, WIDTH];
        private readonly Random rand = new Random();

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

            InitializePopulation();
            DrawField();
        }


        private void InitializePopulation()
        {
            for (int i = 0; i < HEIGHT; i++)
            {
                for (int j = 0; j < WIDTH; j++)
                {
                    bool hasImmunity = rand.Next(1, 101) <= startImmunityPercent;
                    persons[i, j] = new Person(hasImmunity);
                    if (hasImmunity)
                        persons[i, j].CurrentState = Person.State.Immune;
                }
            }

            for (int i = 0; i < HEIGHT; i++)
            {
                if (rand.Next(1, 101) <= startIllPercent)
                    persons[i, 0].CurrentState = Person.State.Infected;
            }
        }

        private static class Virus
        {
            public static int IncubationPeriod = 2;
            public static int InfectionChance;
            public static int Lethality = 0;
            public static int IllnessDuration = 0;
            public static int ImmunityDuration = 2;
        }

        public class Person
        {
            public enum State
            {
                Healthy,
                Infected,
                Immune,
                Dead
            }

            public State CurrentState { get; set; }
            public bool IsAlive { get; set; }
            public int IllnessTime { get; set; }
            public bool IsVaccinated { get; set; }
            public bool HasImmunity { get; set; }
            public int ImmunityDays { get; set; }
            public int IncubationTime { get; set; }

            public Person(bool hasImmunity)
            {
                CurrentState = hasImmunity ? State.Immune : State.Healthy;
                IsAlive = true;
                IllnessTime = 0;
                IsVaccinated = false;
                HasImmunity = hasImmunity;
                ImmunityDays = 0;
                IncubationTime = 0;
            }

            public bool TryInfect(Person source, int infectionRoll)
            {
                return IsAlive &&
                       CurrentState == State.Healthy &&
                       source.CurrentState == State.Infected &&
                       infectionRoll <= Virus.InfectionChance;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                duration = int.Parse(textBox1.Text);
                Virus.IncubationPeriod = int.Parse(textBox6.Text);
                Virus.InfectionChance = int.Parse(textBox4.Text);
                Virus.Lethality = int.Parse(textBox3.Text);
                Virus.ImmunityDuration = int.Parse(textBox2.Text);
                startImmunityPercent = int.Parse(textBox1.Text);
                startIllPercent = int.Parse(textBox7.Text);
                illnessDeathRate = int.Parse(textBox8.Text);
                Virus.IllnessDuration = int.Parse(textBox9.Text);

                textBox10_TextChanged(null, null); 

                InitializePopulation();
                DrawField();
            }
            catch
            {
                MessageBox.Show("error 001");
            }
        }


        private void textBox10_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (Convert.ToInt32(textBox10.Text) == 1)
                {
                    checkBox7.Checked = true;
                    checkBox1.Checked = false;
                    checkBox2.Checked = false;
                    checkBox3.Checked = false;
                    checkBox4.Checked = false;
                    checkBox5.Checked = true;
                    checkBox6.Checked = true;
                    checkBox8.Checked = false;
                    checkBox9.Checked = true;
                    checkBox10.Checked = false;
                    checkBox11.Checked = false;
                    checkBox12.Checked = false;

                }
                if (Convert.ToInt32(textBox10.Text) == 2)
                {
                    checkBox7.Checked = true;
                    checkBox1.Checked = true;
                    checkBox2.Checked = true;
                    checkBox3.Checked = true;
                    checkBox4.Checked = true;
                    checkBox5.Checked = true;
                    checkBox6.Checked = true;
                    checkBox8.Checked = true;
                    checkBox9.Checked = true;
                    checkBox10.Checked = true;
                    checkBox11.Checked = true;
                    checkBox12.Checked = true;

                }
            }
            catch { }
        }

        private void SimulateInfection()
        {
            int radius = int.Parse(textBox10.Text);
            Person.State[,] newStates = new Person.State[HEIGHT, WIDTH];

            Dictionary<(int, int), CheckBox> directionMap = new Dictionary<(int, int), CheckBox>
    {
        { (-2,  0), checkBox4 },
        { (-1, -1), checkBox12 },
        { (-1,  0), checkBox7 },
        { (-1, +1), checkBox8 },
        { ( 0, -2), checkBox3 },
        { ( 0, -1), checkBox9 },
        { ( 0, +1), checkBox6 },
        { ( 0, +2), checkBox1 },
        { (+1, -1), checkBox10 },
        { (+1,  0), checkBox5 },
        { (+1, +1), checkBox11 },
        { (+2,  0), checkBox2 }
    };

            for (int i = 0; i < HEIGHT; i++)
            {
                for (int j = 0; j < WIDTH; j++)
                {
                    var person = persons[i, j];

                    if (!person.IsAlive)
                    {
                        newStates[i, j] = Person.State.Dead;
                        continue;
                    }

                    // Заболевание и выздоровление
                    if (person.CurrentState == Person.State.Infected)
                    {
                        person.IllnessTime++;
                        if (person.IllnessTime >= Virus.IllnessDuration)
                        {
                            if (rand.Next(1, 101) <= Virus.Lethality)
                            {
                                newStates[i, j] = Person.State.Dead;
                                person.IsAlive = false;
                                continue;
                            }
                            else
                            {
                                newStates[i, j] = Person.State.Immune;
                                person.ImmunityDays = Virus.ImmunityDuration;
                                continue;
                            }
                        }
                        newStates[i, j] = Person.State.Infected;
                    }
                    else if (person.CurrentState == Person.State.Immune)
                    {
                        person.ImmunityDays--;
                        newStates[i, j] = (person.ImmunityDays <= 0) ? Person.State.Healthy : Person.State.Immune;
                    }
                    else
                    {
                        newStates[i, j] = person.CurrentState;
                    }

                    // Попытка заразиться от соседа
                    if (person.CurrentState == Person.State.Healthy && !person.IsVaccinated)
                    {
                        foreach (KeyValuePair<(int, int), CheckBox> entry in directionMap)
                        {
                            if (!entry.Value.Checked) continue;

                            int di = entry.Key.Item1;
                            int dj = entry.Key.Item2;

                            int ni = (i + di + HEIGHT) % HEIGHT; // вертикальная цикличность
                            int nj = j + dj;                     // горизонталь без цикличности

                            if (nj < 0 || nj >= WIDTH) continue; // ограничение границ

                            var neighbor = persons[ni, nj];
                            int roll = rand.Next(1, 101);

                            if (person.TryInfect(neighbor, roll))
                            {
                                newStates[i, j] = Person.State.Infected;
                                break;
                            }
                        }
                    }
                }
            }

            // Применение новых состояний
            for (int i = 0; i < HEIGHT; i++)
            {
                for (int j = 0; j < WIDTH; j++)
                {
                    persons[i, j].CurrentState = newStates[i, j];
                }
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            SimulateInfection();
            DrawField();
        }
        private void DrawField()
        {
            int cellSize = 10;
            Bitmap bmp = new Bitmap(WIDTH * cellSize, HEIGHT * cellSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int i = 0; i < HEIGHT; i++)
                {
                    for (int j = 0; j < WIDTH; j++)
                    {
                        Brush brush = Brushes.White;
                        switch (persons[i, j].CurrentState)
                        {
                            case Person.State.Healthy:
                                brush = new SolidBrush(ColorTranslator.FromHtml("#A5D6A7")); break; // насыщенный зелёный
                            case Person.State.Infected:
                                brush = new SolidBrush(ColorTranslator.FromHtml("#FFCDD2")); break; // нежный красный
                            case Person.State.Immune:
                                brush = new SolidBrush(ColorTranslator.FromHtml("#BBDEFB")); break; // мягкий синий
                            case Person.State.Dead:
                                brush = new SolidBrush(ColorTranslator.FromHtml("#2196F3")); break;
                        }
                        g.FillRectangle(brush, j * cellSize, i * cellSize, cellSize - 1, cellSize - 1);
                    }
                }
            }
            pictureBox1.Image = bmp;
        }
        private Timer timer1 = new Timer();
        private void Timer1_Tick(object sender, EventArgs e)
        {
            SimulateInfection();
            DrawField();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void button4_Click(object sender, EventArgs e)
        {
           timer1.Stop();
        }
    }
}
