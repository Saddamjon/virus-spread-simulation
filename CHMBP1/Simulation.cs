using System;
using System.Collections.Generic;

namespace CHMBP1
{
    public enum PersonState
    {
        Healthy,
        Infected,
        Immune,
        Dead
    }

    /// <summary>Смещение соседней клетки: влияет ли она на заражение (строка, столбец).</summary>
    public struct Offset
    {
        public readonly int Di;
        public readonly int Dj;

        public Offset(int di, int dj)
        {
            Di = di;
            Dj = dj;
        }
    }

    /// <summary>Параметры вируса и эксперимента (все вероятности в процентах 0-100).</summary>
    public sealed class VirusParameters
    {
        public int DurationDays = 100;
        public int IncubationDays = 0;
        public int InfectionChance = 100;
        public int Lethality = 0;
        public int ImmunityDays = 5;
        public int InitialImmunePercent = 0;
        public int InitialInfectedPercent = 1;
        public int BorderDisappearPercent = 0;
        public int IllnessDays = 3;

        /// <summary>Возвращает текст ошибки или null, если параметры корректны.</summary>
        public string Validate()
        {
            const int maxDays = 100000;

            if (DurationDays < 1 || DurationDays > maxDays)
                return "Длительность эксперимента должна быть от 1 до " + maxDays + " дней.";
            if (IncubationDays < 0 || IncubationDays > maxDays)
                return "Инкубационный период должен быть от 0 до " + maxDays + " дней.";
            if (IllnessDays < 0 || IllnessDays > maxDays)
                return "Длительность болезни должна быть от 0 до " + maxDays + " дней.";
            if (ImmunityDays < 0 || ImmunityDays > maxDays)
                return "Период иммунитета должен быть от 0 до " + maxDays + " дней.";

            if (!IsPercent(InfectionChance))
                return "Вероятность заражения должна быть от 0 до 100 %.";
            if (!IsPercent(Lethality))
                return "Вероятность смерти должна быть от 0 до 100 %.";
            if (!IsPercent(InitialImmunePercent))
                return "Начальный % иммунитета должен быть от 0 до 100.";
            if (!IsPercent(InitialInfectedPercent))
                return "Начальный % больных должен быть от 0 до 100.";
            if (!IsPercent(BorderDisappearPercent))
                return "Исчезновение больных с границ должно быть от 0 до 100 %.";

            return null;
        }

        private static bool IsPercent(int value)
        {
            return value >= 0 && value <= 100;
        }
    }

    public sealed class Person
    {
        public PersonState State = PersonState.Healthy;

        /// <summary>Сколько дней прошло с момента заражения.</summary>
        public int DaysInfected;

        /// <summary>Сколько дней иммунитета осталось.</summary>
        public int ImmunityLeft;
    }

    /// <summary>
    /// Клеточный автомат эпидемии: поле замкнуто по вертикали и ограничено по горизонтали.
    /// Не зависит от интерфейса, поэтому его можно тестировать.
    ///
    /// Жизнь заражённого: сначала инкубационный период (болен, но не заразен), затем
    /// IllnessDays дней болезни (заразен), после чего человек умирает с вероятностью
    /// Lethality или получает иммунитет на ImmunityDays дней, а потом снова становится здоровым.
    /// </summary>
    public sealed class Simulation
    {
        public const int Height = 50;
        public const int Width = 51;

        private readonly Person[,] persons = new Person[Height, Width];
        private readonly VirusParameters parameters;
        private readonly Offset[] neighbors;
        private readonly Random random;

        public Simulation(VirusParameters parameters, IEnumerable<Offset> neighbors, Random random)
        {
            if (parameters == null) throw new ArgumentNullException("parameters");
            if (neighbors == null) throw new ArgumentNullException("neighbors");
            if (random == null) throw new ArgumentNullException("random");

            string error = parameters.Validate();
            if (error != null) throw new ArgumentException(error, "parameters");

            this.parameters = parameters;
            this.neighbors = new List<Offset>(neighbors).ToArray();
            this.random = random;

            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    persons[i, j] = new Person();

            Reset();
        }

        /// <summary>Номер текущего дня (0 до первого шага).</summary>
        public int Day { get; private set; }

        public PersonState GetState(int row, int column)
        {
            return persons[row, column].State;
        }

        public int Count(PersonState state)
        {
            int count = 0;
            foreach (Person person in persons)
                if (person.State == state)
                    count++;
            return count;
        }

        /// <summary>Эпидемия закончилась: заразных больше нет и новых заражений не будет.</summary>
        public bool IsOver
        {
            get { return Count(PersonState.Infected) == 0; }
        }

        /// <summary>Начальное состояние: случайный иммунитет и больные в левом столбце.</summary>
        public void Reset()
        {
            Day = 0;

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    Person person = persons[i, j];
                    person.DaysInfected = 0;
                    person.ImmunityLeft = 0;
                    person.State = PersonState.Healthy;

                    if (Roll(parameters.InitialImmunePercent))
                    {
                        person.State = PersonState.Immune;
                        person.ImmunityLeft = parameters.ImmunityDays;
                    }
                }
            }

            // Эпидемия начинается с левой границы поля.
            bool anyInfected = false;
            for (int i = 0; i < Height; i++)
            {
                if (persons[i, 0].State == PersonState.Healthy && Roll(parameters.InitialInfectedPercent))
                {
                    Infect(persons[i, 0]);
                    anyInfected = true;
                }
            }

            // При малом проценте в левом столбце может не оказаться больных, и опыт
            // «ничего не происходит». Если больные заданы, гарантируем хотя бы одного.
            if (!anyInfected && parameters.InitialInfectedPercent > 0)
                Infect(persons[random.Next(Height), 0]);
        }

        /// <summary>Один день: заражение, течение болезни, выздоровление, потеря иммунитета.</summary>
        public void Step()
        {
            Day++;

            // Кто заразен утром: решение принимается по состоянию на начало дня, поэтому
            // результат не зависит от порядка обхода клеток.
            bool[,] contagious = new bool[Height, Width];
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    contagious[i, j] = IsContagious(persons[i, j]);

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    Person person = persons[i, j];
                    switch (person.State)
                    {
                        case PersonState.Infected:
                            AdvanceInfected(person, j);
                            break;

                        case PersonState.Immune:
                            person.ImmunityLeft--;
                            if (person.ImmunityLeft <= 0)
                                person.State = PersonState.Healthy;
                            break;

                        case PersonState.Healthy:
                            if (ExposedToInfection(i, j, contagious))
                                Infect(person);
                            break;
                    }
                }
            }
        }

        private void AdvanceInfected(Person person, int column)
        {
            bool onBorder = column == 0 || column == Width - 1;
            if (onBorder && Roll(parameters.BorderDisappearPercent))
            {
                // Больной покинул поле через границу.
                person.State = PersonState.Healthy;
                person.DaysInfected = 0;
                return;
            }

            person.DaysInfected++;
            if (person.DaysInfected < parameters.IncubationDays + parameters.IllnessDays)
                return;

            person.DaysInfected = 0;
            if (Roll(parameters.Lethality))
            {
                person.State = PersonState.Dead;
            }
            else
            {
                person.State = PersonState.Immune;
                person.ImmunityLeft = parameters.ImmunityDays;
            }
        }

        private bool ExposedToInfection(int row, int column, bool[,] contagious)
        {
            foreach (Offset offset in neighbors)
            {
                int ni = (row + offset.Di % Height + Height) % Height; // по вертикали поле замкнуто
                int nj = column + offset.Dj;                            // по горизонтали границы жёсткие
                if (nj < 0 || nj >= Width)
                    continue;

                // Каждый заразный сосед заражает независимо с вероятностью InfectionChance.
                if (contagious[ni, nj] && Roll(parameters.InfectionChance))
                    return true;
            }
            return false;
        }

        private bool IsContagious(Person person)
        {
            return person.State == PersonState.Infected && person.DaysInfected >= parameters.IncubationDays;
        }

        private void Infect(Person person)
        {
            person.State = PersonState.Infected;
            person.DaysInfected = 0;
            person.ImmunityLeft = 0;
        }

        /// <summary>true с вероятностью percent процентов.</summary>
        private bool Roll(int percent)
        {
            return random.Next(100) < percent;
        }
    }
}
