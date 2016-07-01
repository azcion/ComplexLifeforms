﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ComplexLifeforms.Enums;
using static ComplexLifeforms.Utils;

namespace ComplexLifeforms {

	[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
	[SuppressMessage ("ReSharper", "ConvertToAutoPropertyWhenPossible")]
	public class Lifeform {

		/// <summary>Column separator for ToString and ToStringHeader methods. Default=' '</summary>
		public static char Separator = ' ';

		/// <summary>Column width limit for ToString and ToStringHeader methods. Default=0</summary>
		public static int TruncateTo = 0;

		/// <summary>Adds additional data to ToString and ToStringHeader methods. Default=false</summary>
		public static bool Extended = false;

		/// <summary>Enables calling and saving ToString in every update cycle. Default=false</summary>
		public static bool Logging = false;

		/// <summary>Unique ID assigned to this object.</summary>
		public readonly int Id;

		public readonly int ParentIdA;
		public readonly int ParentIdB;

		public readonly Species Species;

		/// <summary>Log of ToString for every update.</summary>
		public readonly HashSet<string> Log;

		/// <summary>Mood, urge and emotion processor.</summary>
		[SuppressMessage ("ReSharper", "InconsistentNaming")]
		public readonly MoodManager MM;

		/// <summary>World in which resources will be exchanged.</summary>
		public readonly World World;

		public bool Breeding;

		private static int _id;

		/// <summary>Constructor parameters.</summary>
		private readonly InitLifeform _init;

		private readonly int _healThreshold;
		private readonly int _sleepThreshold;
		private readonly int _eatThreshold;
		private readonly int _drinkThreshold;

		private readonly int _hpDrain;
		private readonly int _energyDrain;
		private readonly int _foodDrain;
		private readonly int _waterDrain;

		private readonly int _healCost;
		private readonly int _healAmount;

		private readonly int _eatChance;
		private readonly int _eatChanceRangeLower;
		private readonly int _eatChanceRangeUpper;
		private readonly int _drinkChance;
		private readonly int _drinkChanceRangeLower;
		private readonly int _drinkChanceRangeUpper;

		private int _hp;
		private int _energy;
		private int _food;
		private int _water;

		private DeathBy _deathBy;
		private bool _alive;
		private bool _pendingKill;

		private int _age;
		private int _healCount;
		private int _sleepCount;
		private int _eatCount;
		private int _drinkCount;
		private int _breedCount;

		public Lifeform (World world, Species species) {
			Id = _id++;
			ParentIdA = -1;
			ParentIdB = -1;
			Species = species;
			Log = Logging ? new HashSet<string>() : null;
			World = world;
			MM = new MoodManager(this);

			_init = SpeciesContainer.INIT[species];
			_alive = true;
			_deathBy = DeathBy.None;

			InitWorld w = world.Init;
			InitLifeform l = _init;

			_healCost = (int) (w.HealCost * l.HealCostScale);
			_healAmount = (int) (w.HealAmount * l.HealAmountScale);

			_hpDrain = (int) (w.HpDrain * l.HpDrainScale);
			_energyDrain = (int) (w.EnergyDrain * l.EnergyDrainScale);
			_foodDrain = (int) (w.FoodDrain * l.FoodDrainScale);
			_waterDrain = (int) (w.WaterDrain * l.WaterDrainScale);

			_hp = (int) (w.BaseHp * l.HpScale);
			_energy = (int) (w.BaseEnergy * l.EnergyScale);
			_food = (int) (w.BaseFood * l.FoodScale);
			_water = (int) (w.BaseWater * l.WaterScale);

			_healThreshold = (int) (_hp * l.HealThreshold);
			_sleepThreshold = (int) (_energy * l.SleepThreshold);
			_eatThreshold = (int) (_food * l.EatThreshold);
			_drinkThreshold = (int) (_water * l.DrinkThreshold);

			switch (species) {
				case Species.Alpha:
				case Species.Beta:
					_eatChance = 2;
					_eatChanceRangeLower = 5;
					_eatChanceRangeUpper = 10;
					_drinkChance = 2;
					_drinkChanceRangeLower = 1;
					_drinkChanceRangeUpper = 10;
					break;
				case Species.Gamma:
					_eatChance = 1;
					_eatChanceRangeLower = 5;
					_eatChanceRangeUpper = 10;
					_drinkChance = 1;
					_drinkChanceRangeLower = 5;
					_drinkChanceRangeUpper = 10;
					break;
			}
		}

		/// <summary>
		/// Constructor for making a new child based on two parents.
		/// </summary>
		private Lifeform (World world, Species species, MoodManager mood, int parentIdA, int parentIdB)
				: this(world, species) {
			ParentIdA = parentIdA;
			ParentIdB = parentIdB;
			MM = mood;
		}

		public int Age => _age;
		public bool Alive => _alive;
		public DeathBy DeathBy => _deathBy;
		public int BreedCount => _breedCount;

		public static Lifeform Breed (Lifeform parentA, Lifeform parentB) {
			if (!IsQualified(parentA) || !IsQualified(parentB)) {
				return null;
			}

			int[] dna = MixDNA();

			Tier[] urgeBias = new Tier[URGE_COUNT];
			Tier[] emotionBias = new Tier[EMOTION_COUNT];

			for (int i = 0; i < URGE_COUNT; ++i) {
				Lifeform parent = dna[i] == 0 ? parentA : parentB;
				urgeBias[i] = parent.MM.UrgeBias[i];
			}

			for (int i = 0; i < EMOTION_COUNT; ++i) {
				Lifeform parent = dna[i + URGE_COUNT] == 0 ? parentA : parentB;
				emotionBias[i] = parent.MM.EmotionBias[i];
			}

			MoodManager mood = new MoodManager(urgeBias, emotionBias);
			Lifeform child = new Lifeform(parentA.World, parentA.Species, mood, parentA.Id, parentB.Id) {
				_food = parentA._foodDrain + parentB._foodDrain,
				_water = parentA._waterDrain + parentB._waterDrain
			};

			BreedAction(parentA);
			BreedAction(parentB);

			return child;
		}

		public static string ToStringHeader () {
			char s = Separator;
			string data = $"species{s}age  {s}hp   {s}energ{s}food {s}water";

			if (!Extended) {
				return data;
			}

			if (TruncateTo == 0) {
				data += $"{s}heals{s}slept{s}eaten{s}drank{s}{"urge",-9}{s}{"emotion",-12}"
						+ $"{s}{"mood",-8}{s}{"death by",-13}{s}sleeping";
			} else {
				data += $"{s}heals{s}slept{s}eaten{s}drank{s}bred ";

				string[] elements = {
					"urge", "emotion", "mood", "death by", "sleeping"
				};

				foreach (string element in elements) {
					data += s + Truncate(element, TruncateTo, 1);
				}
			}

			return data;
		}

		public void Update () {
			if (!_alive) {
				return;
			}

			if (_pendingKill) {
				Kill();
				return;
			}

			ProcessBodilyFunctions();
			Forage();
			Heal();
			ProcessSleep();
			ClampValues();
			MM.Update();

			Breeding = false;
			++_age;

			if (_hp <= 0 || _deathBy != DeathBy.None || _pendingKill) {
				if (_deathBy != DeathBy.None && !_pendingKill) {
					Console.WriteLine("problem");
					Console.ReadLine();
				}
				Kill();
			}

			if (Logging) {
				Log.Add(ToString());
			}
		}

		public void Eat (int amount) {
			if (!_alive || MM.Asleep || _pendingKill) {
				return;
			}

			int deltaEnergy = 0;
			int deltaFood = 0;
			int deltaWater = 0;

			if (World.Food < amount) {
				amount = World.Food;
			}

			if (_food < _eatThreshold) {
				deltaEnergy -= _energyDrain;
				deltaFood += amount;
				deltaWater -= _waterDrain / 2;
			} else {
				deltaEnergy -= _energyDrain / 2;
				deltaFood += amount / 2;
				deltaWater -= _waterDrain * 2;
			}

			if (_water <= -deltaWater) {
				MM.AffectUrge(Urge.Drink, 1);
				return;
			}

			World.UseFood(deltaFood);
			World.Reclaim(0, -deltaWater);
			MM.Action(Urge.Eat);

			_energy += deltaEnergy;
			_food += deltaFood;
			_water += deltaWater;
			++_eatCount;

			if (_food > _init.Food) {
				_hp -= _hpDrain * 4;

				MM.AffectUrge(Urge.Eat, -2);
				MM.AffectUrge(Urge.Excrete, 1);
			}

			if (_hp <= 0 && _deathBy == DeathBy.None) {
				_deathBy = DeathBy.Gluttony;
				_pendingKill = true;
			}
		}

		public void Drink (int amount) {
			if (!_alive || MM.Asleep || _pendingKill) {
				return;
			}

			int deltaEnergy = 0;
			int deltaWater = 0;

			if (World.Water < amount) {
				amount = World.Water;
			}

			if (_water < _drinkThreshold) {
				deltaEnergy -= _energyDrain;
				deltaWater += amount;
			} else {
				deltaEnergy -= _energyDrain / 2;
				deltaWater += amount / 2;
			}

			World.UseWater(deltaWater);
			MM.Action(Urge.Drink);

			_energy += deltaEnergy;
			_water += deltaWater;
			++_drinkCount;

			if (_water > _init.Water) {
				_hp -= _hpDrain * 4;

				MM.AffectUrge(Urge.Drink, -2);
				MM.AffectUrge(Urge.Excrete, 1);
			}

			if (_hp <= 0 && _deathBy == DeathBy.None) {
				_deathBy = DeathBy.Overdrinking;
				_pendingKill = true;
			}
		}

		public override string ToString () {
			char s = Separator;
			string data = $"{Species,-7}{s}{_age,5}{s}{_hp,5}{s}{_energy,5}{s}{_food,5}{s}{_water,5}";

			if (!Extended) {
				return data;
			}

			if (TruncateTo == 0) {
				data += $"{s}{_healCount,5}{s}{_sleepCount,5}{s}{_eatCount,5}{s}{_drinkCount,5}"
						+ $"{s}{MM.Urge,-9}{s}{MoodManager.EmotionName(MM),-12}{s}{MM.Mood,-8}"
						+ $"{s}{_deathBy,-13}{s}{(MM.Asleep ? "yes" : "no"),-6}";
			} else {
				data += $"{s}{_healCount,5}{s}{_sleepCount,5}{s}{_eatCount,5}{s}{_drinkCount,5}{s}{_breedCount,5}";

				object[] elements = {
						MM.Urge, MoodManager.EmotionName(MM), MM.Mood, _deathBy, MM.Asleep ? "yes" : "no"
				};

				foreach (object element in elements) {
					data += s + Truncate(element.ToString(), TruncateTo, 1);
				}
			}

			return data;
		}

		private static bool IsQualified (Lifeform parent) {
			bool unqualified = parent._hp < parent._init.HealThreshold
					|| parent._food < parent._foodDrain
					|| parent._water < parent._waterDrain;

			return !unqualified;
		}

		private static void BreedAction (Lifeform parent) {
			parent._hp /= 2;
			parent._energy /= 2;
			parent._food -= parent._foodDrain;
			parent._water -= parent._waterDrain;
			parent._breedCount++;
			parent.MM.Action(Urge.Reproduce);
		}

		private static DeathBy DeltaDeathBy (DeathBy causeW, DeathBy causeF) {
			DeathBy cause;

			if (causeW == DeathBy.None) {
				cause = causeF;
			} else {
				if (causeF == DeathBy.None)
					cause = causeW;
				else
					cause = DeathBy.Malnutrition;
			}

			return cause;
		}

		private void Forage () {
			if (MM.Asleep || Breeding) {
				return;
			}

			if (Utils.Random.Next(_eatChance) == 0) {
				Eat(Utils.Random.Next(_eatChanceRangeLower, _eatChanceRangeUpper) * _foodDrain * 3);
			}

			if (Utils.Random.Next(_drinkChance) == 0) {
				Drink(Utils.Random.Next(_drinkChanceRangeLower, _drinkChanceRangeUpper) * _waterDrain * 3);
			}
		}

		private void ProcessBodilyFunctions () {
			bool excreteW = false;
			bool excreteF = false;
			int dHp = 0;
			int dEnergy = 0;
			int dFood = 0;
			int dWater = 0;
			DeathBy causeW = DeathBy.None;
			DeathBy causeF = DeathBy.None;

			DeltaWater(ref dHp, ref dEnergy, ref dWater, ref causeW, ref excreteW);
			DeltaFood(ref dHp, ref dEnergy, ref dFood, ref causeF, ref excreteF);

			if (MM.Asleep) {
				// don't lose resources while asleep
				dEnergy = 0;
				dFood = 0;
				dWater = 0;
			} else {
				if (excreteW) {
					MM.AffectUrge(Urge.Drink, -1);
					MM.Action(Urge.Excrete);
				}

				if (excreteF) {
					MM.AffectUrge(Urge.Eat, -1);
					MM.Action(Urge.Excrete);
				}
			}

			World.Reclaim(-dFood, -dWater);

			_hp += dHp;
			_energy += dEnergy;
			_food += dFood;
			_water += dWater;

			if (_hp < 0 && _deathBy == DeathBy.None) {
				_deathBy = DeltaDeathBy(causeW, causeF);
				_pendingKill = true;
			}
		}

		private void DeltaWater (ref int hp, ref int energy, ref int water,
				ref DeathBy cause, ref bool excrete) {
			if (_water > _init.Water) {
				excrete = true;
				hp -= _hpDrain * 8;
				energy -= _energyDrain * 8;
				water -= _waterDrain * 8;
				cause = DeathBy.Overdrinking;
			} else if (_water > _drinkThreshold) {
				excrete = true;
				energy -= _energyDrain * 4;
				water -= _waterDrain * 4;
			} else if (_water > _waterDrain) {
				energy -= _energyDrain;
				water -= _waterDrain;
			} else if (_water > 0) {
				hp -= _hpDrain * 4;
				energy -= _energyDrain * 2;
				water -= _water;
				cause = DeathBy.Dehydration;
			} else {
				hp -= _hpDrain * 16;
				energy -= _energyDrain * 4;

				if (cause == DeathBy.None) {
					cause = DeathBy.Dehydration;
				}
			}
		}

		private void DeltaFood (ref int hp, ref int energy, ref int food,
				ref DeathBy cause, ref bool excrete) {
			if (_food > _init.Food) {
				excrete = true;
				hp -= _hpDrain * 8;
				energy -= _energyDrain * 8;
				food -= _foodDrain * 8;
				cause = DeathBy.Gluttony;
			} else if (_food > _eatThreshold) {
				excrete = true;
				energy -= _energyDrain * 4;
				food -= _foodDrain * 4;
			} else if (_food > _foodDrain) {
				energy -= _energyDrain;
				food -= _foodDrain;
			} else if (_food > 0) {
				hp -= _hpDrain * 4;
				energy -= _energyDrain * 2;
				food -= _food;
				cause = DeathBy.Starvation;
			} else {
				hp -= _hpDrain * 16;
				energy -= _energyDrain * 4;

				if (cause == DeathBy.None) {
					cause = DeathBy.Starvation;
				}
			}
		}

		private void ClampValues () {
			if (_energy < 0) {
				_energy = 0;
			}

			if (_food < 0) {
				_food = 0;
			}

			if (_water < 0) {
				_water = 0;
			}
		}

		private void Heal () {
			if (_hp > _healThreshold || MM.Asleep || _pendingKill) {
				return;
			}

			int deltaEnergy = 0;
			int deltaFood = 0;
			int deltaWater = 0;
			int cost = _healCost + _age;

			switch (MM.Mood) {
				case Enums.Mood.Great:
					cost -= _healCost;
					break;
				case Enums.Mood.Good:
					cost -= _healCost / 2;
					break;
				case Enums.Mood.Bad:
					cost += _healCost / 2;
					break;
				case Enums.Mood.Terrible:
					cost += _healCost;
					break;
			}

			int effectiveness = cost + cost;

			if (_food <= cost) {
				MM.AffectUrge(Urge.Eat, 1);

				if (_food > 0) {
					deltaEnergy -= cost / 2;
					deltaFood -= _food;
					effectiveness -= _food;
				} else {
					effectiveness -= cost;
				}
			} else {
				deltaEnergy -= cost;
				deltaFood -= cost;
			}

			if (_water <= cost) {
				MM.AffectUrge(Urge.Drink, 1);

				if (_water > 0) {
					deltaEnergy -= cost / 2;
					deltaWater -= _water;
					effectiveness -= _water;
				} else {
					effectiveness -= cost;
				}
			} else {
				deltaEnergy -= cost;
				deltaWater -= cost;
			}

			if (effectiveness == 0) {
				return;
			}

			World.Reclaim(-deltaFood, -deltaWater);
			MM.Action(Urge.Heal);
			MM.AffectUrge(Urge.Sleep, 2);

			_hp += effectiveness / (cost + cost) * _healAmount;
			_energy += deltaEnergy;
			_food += deltaFood;
			_water += deltaWater;
			++_healCount;
		}

		private void ProcessSleep () {
			if (_pendingKill) {
				return;
			}

			if (MM.Asleep) {
				_hp -= _hpDrain;
				_energy += _energyDrain * 8;
				++_sleepCount;

				if (_energy >= _init.Energy || _hp < _hpDrain * 32) {
					MM.Asleep = false;
				}

				return;
			}

			if (_energy < _sleepThreshold) {
				MM.Asleep = true;
				MM.Action(Urge.Sleep);

				if (_energy <= 0) {
					_hp -= _hpDrain * 10;

					if (_hp < 0 && _deathBy == DeathBy.None) {
						_deathBy = DeathBy.Exhaustion;
						_pendingKill = true;
					}
				}
			}
		}

		private void Kill () {
			_pendingKill = false;
			_alive = false;

			if (_deathBy == DeathBy.None) {
				if (_energy <= 0) {
					_deathBy = DeathBy.Exhaustion;
				} else if (_food <= 0) {
					_deathBy = DeathBy.Starvation;
				} else if (_water <= 0) {
					_deathBy = DeathBy.Dehydration;
				}
			}

			_hp = -1;
			World.Decompose(_food, _water);
		}

	}

}