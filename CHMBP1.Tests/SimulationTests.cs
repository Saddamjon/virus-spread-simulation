using CHMBP1;

namespace CHMBP1.Tests
{
    public class SimulationTests
    {
        private static readonly Offset[] Radius1 =
        {
            new Offset(-1, 0), new Offset(1, 0), new Offset(0, -1), new Offset(0, 1)
        };

        private static readonly Offset[] NoNeighbors = new Offset[0];

        /// <summary>Параметры, при которых больные только в левом столбце и ничего не случайно.</summary>
        private static VirusParameters Deterministic()
        {
            return new VirusParameters
            {
                DurationDays = 100,
                IncubationDays = 0,
                IllnessDays = 3,
                InfectionChance = 100,
                Lethality = 0,
                ImmunityDays = 5,
                InitialImmunePercent = 0,
                InitialInfectedPercent = 100,
                BorderDisappearPercent = 0
            };
        }

        private static Simulation Create(VirusParameters parameters, Offset[] neighbors, int seed = 1)
        {
            return new Simulation(parameters, neighbors, new Random(seed));
        }

        private static int InColumn(Simulation simulation, int column, PersonState state)
        {
            int count = 0;
            for (int i = 0; i < Simulation.Height; i++)
                if (simulation.GetState(i, column) == state)
                    count++;
            return count;
        }

        private static void Run(Simulation simulation, int days)
        {
            for (int d = 0; d < days; d++)
                simulation.Step();
        }

        [Fact]
        public void Start_always_has_a_sick_person_when_percent_is_positive()
        {
            // При 1 % на 50 строк в левом столбце часто не оказывалось ни одного больного.
            VirusParameters parameters = Deterministic();
            parameters.InitialInfectedPercent = 1;

            for (int seed = 0; seed < 300; seed++)
                Assert.True(Create(parameters, Radius1, seed).Count(PersonState.Infected) >= 1, "seed " + seed);
        }

        [Fact]
        public void Start_has_nobody_sick_when_percent_is_zero()
        {
            VirusParameters parameters = Deterministic();
            parameters.InitialInfectedPercent = 0;

            Simulation simulation = Create(parameters, Radius1);

            Assert.Equal(0, simulation.Count(PersonState.Infected));
            Assert.True(simulation.IsOver);
        }

        [Fact]
        public void Sick_people_start_only_in_the_left_column()
        {
            Simulation simulation = Create(Deterministic(), Radius1);

            Assert.Equal(Simulation.Height, InColumn(simulation, 0, PersonState.Infected));
            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Infected));
        }

        [Fact]
        public void Initial_immunity_does_not_vanish_after_the_first_day()
        {
            VirusParameters parameters = Deterministic();
            parameters.InitialInfectedPercent = 0;
            parameters.InitialImmunePercent = 100;
            parameters.ImmunityDays = 3;
            Simulation simulation = Create(parameters, Radius1);
            int total = Simulation.Height * Simulation.Width;

            Run(simulation, 2);
            Assert.Equal(total, simulation.Count(PersonState.Immune));

            Run(simulation, 1);
            Assert.Equal(total, simulation.Count(PersonState.Healthy));
        }

        [Fact]
        public void Infection_spreads_one_cell_per_day()
        {
            Simulation simulation = Create(Deterministic(), Radius1);

            simulation.Step();

            Assert.Equal(Simulation.Height, InColumn(simulation, 1, PersonState.Infected));
            Assert.Equal(Simulation.Height, InColumn(simulation, 2, PersonState.Healthy));
        }

        [Fact]
        public void Zero_infection_chance_never_spreads()
        {
            VirusParameters parameters = Deterministic();
            parameters.InfectionChance = 0;
            Simulation simulation = Create(parameters, Radius1);

            Run(simulation, 2);

            Assert.Equal(0, InColumn(simulation, 1, PersonState.Infected));
        }

        [Fact]
        public void Without_neighbors_the_virus_cannot_spread()
        {
            Simulation simulation = Create(Deterministic(), NoNeighbors);

            Run(simulation, 2);

            Assert.Equal(0, InColumn(simulation, 1, PersonState.Infected));
        }

        [Fact]
        public void Incubation_delays_contagiousness()
        {
            VirusParameters parameters = Deterministic();
            parameters.IncubationDays = 2;
            parameters.IllnessDays = 3;
            Simulation simulation = Create(parameters, Radius1);

            Run(simulation, 2);
            Assert.Equal(0, InColumn(simulation, 1, PersonState.Infected));

            simulation.Step();
            Assert.Equal(Simulation.Height, InColumn(simulation, 1, PersonState.Infected));
        }

        [Fact]
        public void Illness_lasts_the_configured_number_of_days_then_gives_immunity()
        {
            VirusParameters parameters = Deterministic();
            parameters.IllnessDays = 3;
            Simulation simulation = Create(parameters, NoNeighbors);

            Run(simulation, 2);
            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Infected));

            simulation.Step();
            Assert.Equal(0, simulation.Count(PersonState.Infected));
            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Immune));
            Assert.True(simulation.IsOver);
        }

        [Fact]
        public void Immunity_wears_off_after_the_configured_period()
        {
            VirusParameters parameters = Deterministic();
            parameters.IllnessDays = 1;
            parameters.ImmunityDays = 2;
            Simulation simulation = Create(parameters, NoNeighbors);

            simulation.Step(); // выздоровели: иммунитет 2 дня
            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Immune));

            simulation.Step();
            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Immune));

            simulation.Step();
            Assert.Equal(0, simulation.Count(PersonState.Immune));
            Assert.Equal(Simulation.Height * Simulation.Width, simulation.Count(PersonState.Healthy));
        }

        [Fact]
        public void Full_lethality_kills_everyone_who_was_sick()
        {
            VirusParameters parameters = Deterministic();
            parameters.Lethality = 100;
            Simulation simulation = Create(parameters, NoNeighbors);

            Run(simulation, 3);

            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Dead));
            Assert.Equal(0, simulation.Count(PersonState.Immune));
        }

        [Fact]
        public void Zero_lethality_kills_nobody()
        {
            Simulation simulation = Create(Deterministic(), Radius1);

            Run(simulation, 30);

            Assert.Equal(0, simulation.Count(PersonState.Dead));
        }

        [Fact]
        public void Dead_stay_dead()
        {
            VirusParameters parameters = Deterministic();
            parameters.Lethality = 100;
            Simulation simulation = Create(parameters, NoNeighbors);
            Run(simulation, 3);

            Run(simulation, 50);

            Assert.Equal(Simulation.Height, simulation.Count(PersonState.Dead));
        }

        [Fact]
        public void Sick_people_on_the_border_can_disappear()
        {
            VirusParameters parameters = Deterministic();
            parameters.BorderDisappearPercent = 100;
            Simulation simulation = Create(parameters, NoNeighbors);

            simulation.Step();

            Assert.Equal(0, simulation.Count(PersonState.Infected));
            Assert.Equal(0, simulation.Count(PersonState.Immune));
            Assert.Equal(Simulation.Height * Simulation.Width, simulation.Count(PersonState.Healthy));
        }

        [Fact]
        public void Same_seed_gives_same_epidemic()
        {
            VirusParameters parameters = Deterministic();
            parameters.InitialInfectedPercent = 10;
            parameters.InfectionChance = 40;
            parameters.Lethality = 20;

            Simulation first = Create(parameters, Radius1, 7);
            Simulation second = Create(parameters, Radius1, 7);
            Run(first, 25);
            Run(second, 25);

            foreach (PersonState state in Enum.GetValues(typeof(PersonState)))
                Assert.Equal(first.Count(state), second.Count(state));
        }

        [Fact]
        public void Population_is_conserved()
        {
            VirusParameters parameters = Deterministic();
            parameters.InitialInfectedPercent = 10;
            parameters.InfectionChance = 50;
            parameters.Lethality = 30;
            Simulation simulation = Create(parameters, Radius1);

            for (int day = 0; day < 40; day++)
            {
                simulation.Step();
                int total = 0;
                foreach (PersonState state in Enum.GetValues(typeof(PersonState)))
                    total += simulation.Count(state);
                Assert.Equal(Simulation.Height * Simulation.Width, total);
            }
        }

        [Fact]
        public void Day_counter_advances_and_reset_restarts_it()
        {
            Simulation simulation = Create(Deterministic(), Radius1);

            Run(simulation, 4);
            Assert.Equal(4, simulation.Day);

            simulation.Reset();
            Assert.Equal(0, simulation.Day);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void Percent_fields_must_be_between_0_and_100(int value)
        {
            VirusParameters parameters = Deterministic();
            parameters.Lethality = value;

            Assert.NotNull(parameters.Validate());
            Assert.Throws<ArgumentException>(() => Create(parameters, Radius1));
        }

        [Fact]
        public void Duration_must_be_positive()
        {
            VirusParameters parameters = Deterministic();
            parameters.DurationDays = 0;

            Assert.NotNull(parameters.Validate());
        }

        [Fact]
        public void Default_parameters_are_valid()
        {
            Assert.Null(new VirusParameters().Validate());
        }
    }
}
