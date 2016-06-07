﻿using System;
using System.Collections.Generic;
using ComplexLifeforms.Enums;

namespace ComplexLifeforms {

	public class MoodManager {

		public Lifeform Lifeform;

		/// <summary>Represents the current strongest urge.</summary>
		public Urge Urge { get; private set; }
		/// <summary>Represents the current strongest emotion.</summary>
		public Emotion Emotion { get; private set; }

		public int[] UrgeValues { get; private set;}
		public int[] EmotionValues { get; private set; }

		public Tier[] UrgeBias { get; private set; }
		public Tier[] EmotionBias { get; private set; }

		protected internal bool Asleep;

		private readonly Random _random;

		private static readonly int[,] TYPE_VALUES = { { 1, 1, 1 }, { 2, 1, 1 }, { 3, 2, 1 } };
		private static readonly string[,] EMOTION_NAMES = {
				{ "Serenity", "Acceptance", "Apprehension", "Distraction",
						"Pensiveness", "Boredom", "Annoyance", "Interest" },
				{ "Joy", "Trust", "Fear", "Surprise",
						"Sadness", "Disgust", "Anger", "Anticipation" },
				{ "Ecstasy", "Admiration", "Terror", "Amazement",
						"Grief", "Loathing", "Rage", "Vigilance" }
		};

		public static readonly int URGE_COUNT = Enum.GetNames(typeof(Urge)).Length;
		public static readonly int EMOTION_COUNT = Enum.GetNames(typeof(Emotion)).Length;
		public static readonly int TIER_COUNT = Enum.GetNames(typeof(Tier)).Length;

		public const int URGE_CAP = 99;
		public const int EMOTION_CAP = 99;

		public MoodManager (Lifeform lifeform, Random random=null) {
			Lifeform = lifeform;
			_random = random ?? new Random();

			Asleep = false;

			UrgeValues = new int[URGE_COUNT];
			EmotionValues = new int[EMOTION_COUNT];

			UrgeBias = new Tier[URGE_COUNT];
			EmotionBias = new Tier[EMOTION_COUNT];

			for (int i = 0; i < URGE_COUNT; ++i) {
				UrgeBias[i] = (Tier) _random.Next(TIER_COUNT);
			}

			for (int i = 0; i < EMOTION_COUNT; ++i) {
				EmotionBias[i] = (Tier) _random.Next(TIER_COUNT);
			}
		}

		public void Update () {
			ProcessChanges();
			ClampValues();

			Urge = (Urge) MaxIndex(UrgeValues);
			Emotion = (Emotion) MaxIndex(EmotionValues);
		}

		private void ProcessChanges () {
			for (int i = 0; i < URGE_COUNT; ++i) {
				if (_random.Next((int) UrgeBias[i], TIER_COUNT + 1) == TIER_COUNT) {
					if (Asleep) {
						--UrgeValues[i];
					} else {
						++UrgeValues[i];
					}
				}
			}

			for (int i = 0; i < EMOTION_COUNT; ++i) {
				if (Asleep) {
					EmotionValues[i] -= TIER_COUNT - (int) EmotionBias[i];
				} else if (_random.Next((int) EmotionBias[i], TIER_COUNT + 1) == TIER_COUNT) {
					--EmotionValues[i];
				}
			}
		}

		private void AffectEmotions (IReadOnlyList<Emotion> emotions, int type) {
			for (int i = 0; i < emotions.Count; ++i) {
				EmotionValues[(int) emotions[i]] += TYPE_VALUES[type, i];
			}
		}

		protected internal void ClampValues () {
			for (int i = 0; i < URGE_COUNT; ++i) {
				if (UrgeBias[i] == Tier.None) {
					UrgeValues[i] = 0;
					continue;
				}

				int u = UrgeValues[i];

				if (u < 0) {
					UrgeValues[i] = 0;
				} else if (u > URGE_CAP) {
					UrgeValues[i] = URGE_CAP;
				}
			}

			for (int i = 0; i < EMOTION_COUNT; ++i) {
				if (EmotionBias[i] == Tier.None) {
					EmotionValues[i] = 0;
					continue;
				}

				int e = EmotionValues[i];

				if (e < 0) {
					EmotionValues[i] = 0;
				} else if (e > EMOTION_CAP) {
					EmotionValues[i] = EMOTION_CAP;
				}
			}
		}

		public void Action (Urge action) {
			int iaction = (int) action;
			int[] indexes = EdgeIndexes(UrgeValues);
			int maxA = indexes[0];
			int maxB = indexes[1];
			int minA = indexes[2];
			int minB = indexes[3];

			Emotion[] emotions;
			int type = 0;

			switch (action) {
				case Urge.Eat:
				case Urge.Drink:
					if (iaction == maxA) {  // highest
						emotions = new[] { Emotion.Joy, Emotion.Surprise, Emotion.Trust };
						type = 2;
					} else if (iaction == maxB) {  // second highest
						emotions = new[] { Emotion.Joy, Emotion.Anticipation };
						type = 1;
					} else if (iaction == minA) {  // lowest
						emotions = new[] { Emotion.Disgust, Emotion.Sadness, Emotion.Anger };
						type = 2;
					} else if (iaction == minB) {  // second lowest
						emotions = new[] { Emotion.Disgust, Emotion.Sadness };
						type = 1;
					} else {
						emotions = new[] { Emotion.Anticipation, Emotion.Sadness };
					}
					break;
				case Urge.Excrete:
				case Urge.Reproduce:
					if (iaction == maxA) {  // highest
						emotions = new[] { Emotion.Joy, Emotion.Trust, Emotion.Surprise };
						type = 2;
					} else if (iaction == maxB) {  // second highest
						emotions = new[] { Emotion.Joy, Emotion.Anticipation };
						type = 1;
					} else if (iaction == minA) {  // lowest
						emotions = new[] { Emotion.Disgust, Emotion.Anger, Emotion.Fear };
						type = 2;
					} else if (iaction == minB) {  // second lowest
						emotions = new[] { Emotion.Disgust, Emotion.Anger };
						type = 1;
					} else {
						emotions = new[] { Emotion.Anticipation, Emotion.Sadness };
					}
					break;
				case Urge.Sleep:
					if (iaction == maxA) {  // highest
						emotions = new[] { Emotion.Joy, Emotion.Fear };
						type = 2;
					} else if (iaction == maxB) {  // second highest
						emotions = new[] { Emotion.Joy, Emotion.Fear };
						type = 1;
					} else if (iaction == minA) {  // lowest
						emotions = new[] { Emotion.Anger, Emotion.Joy };
						type = 2;
					} else if (iaction == minB) {  // second lowest
						emotions = new[] { Emotion.Anger, Emotion.Joy };
						type = 1;
					} else {
						emotions = new[] { Emotion.Joy, Emotion.Fear };
					}
					break;
				case Urge.Heal:
					if (iaction == maxA) {  // highest
						emotions = new[] { Emotion.Joy, Emotion.Fear, Emotion.Anticipation };
						type = 2;
					} else if (iaction == maxB) {  // second highest
						emotions = new[] { Emotion.Fear, Emotion.Joy };
						type = 1;
					} else if (iaction == minA) {  // lowest
						emotions = new[] { Emotion.Fear, Emotion.Anticipation, Emotion.Anger };
						type = 2;
					} else if (iaction == minB) {  // second lowest
						emotions = new[] { Emotion.Fear, Emotion.Anticipation };
						type = 1;
					} else {
						emotions = new[] { Emotion.Fear, Emotion.Anticipation };
					}
					break;
				default:
					Console.WriteLine($"Unimplemented action. a:{action}");
					return;
			}

			AffectEmotions(emotions, type);
			UrgeValues[iaction] -= 5;
		}

		public void AffectUrge (Urge urge, int delta) {
			// todo expand
			UrgeValues[(int) urge] += delta;
		}

		public void ApplyTiers (Tier[] urgeBias, Tier[] emotionBias) {
			if (urgeBias.Length != URGE_COUNT || emotionBias.Length != EMOTION_COUNT) {
				Console.WriteLine($"Invalid length of values.  first:{urgeBias.Length} second:{emotionBias.Length}");
				return;
			}

			for (int i = 0; i < URGE_COUNT; ++i) {
				UrgeBias[i] = urgeBias[i];
			}

			for (int i = 0; i < EMOTION_COUNT; ++i) {
				EmotionBias[i] = emotionBias[i];
			}
		}

		public string ToString (char separator=' ') {
			char s = separator;
			string data = $"{UrgeValues[0],2} {(int) UrgeBias[0]}";

			for (int i = 1; i < URGE_COUNT; ++i) {
				data += $"{s}{UrgeValues[i],2} {(int) UrgeBias[i]}";
			}

			data += s;
			for (int i = 0; i < EMOTION_COUNT; ++i) {
				data += $"{s}{EmotionValues[i],2} {(int) EmotionBias[i]}";
			}

			return data;
		}

		public static string ToStringHeader (char separator=' ') {
			char s = separator;
			string data = "";

			bool first = true;
			foreach (string urge in Enum.GetNames(typeof(Urge))) {
				if (first) {
					data += $"{Truncate(urge, 4),-4}";
					first = false;
					continue;
				}

				data += $"{s}{Truncate(urge, 4),-4}";
			}

			data += s;
			foreach (string emotion in Enum.GetNames(typeof(Emotion))) {
				data += $"{s}{Truncate(emotion, 4),-4}";
			}

			return data;
		}

		public static string Truncate (string value, int length) {
			if (string.IsNullOrEmpty(value)) {
				return value;
			}

			if (value.Length <= length) {
				return value;
			}

			return value.Substring(0, length);
		}

		public static int[] EdgeIndexes<T> (IEnumerable<T> array) where T : IComparable<T> {
		public static int[] EdgeIndexes (int[] array) {
			int maxAIndex = -1;
			int maxBIndex = -1;
			int minAIndex = -1;
			int minBIndex = -1;
			int maxAValue = array[0];
			int maxBValue = array[0];
			int minAValue = array[0];
			int minBValue = array[0];

			int index = 0;
			foreach (int value in array) {
				if (value.CompareTo(maxAValue) > 0 || maxAIndex == -1) {
					maxBIndex = maxAIndex;
					maxAIndex = index;
					maxAValue = value;
				} else if (value.CompareTo(maxBValue) > 0 || maxBIndex == -1) {
					maxBIndex = index;
					maxBValue = value;
				}

				if (value.CompareTo(minAValue) < 0 || minBIndex == -1) {
					minBIndex = minAIndex;
					minAIndex = index;
					minAValue = value;
				} else if (value.CompareTo(minBValue) < 0 || minBIndex == -1) {
					minBIndex = index;
					minBValue = value;
				}

				++index;
			}

			int[] indexes = {maxAIndex, maxBIndex, minAIndex, minBIndex};
			return indexes;
		}

		public static int MaxIndex<T> (IEnumerable<T> array) where T : IComparable<T> {
			int maxIndex = -1;
			T maxValue = default(T);

			int index = 0;
			foreach (T value in array) {
				if (value.CompareTo(maxValue) > 0 || maxIndex == -1) {
					maxIndex = index;
					maxValue = value;
				}
				++index;
			}

			return maxIndex;
		}

		public static string EmotionName (Emotion emotion, int value) {
			int intensity = 1;

			if (value >= EMOTION_CAP * 0.75) {
				intensity = 2;
			} else if (value <= EMOTION_CAP * 0.25) {
				intensity = 0;
			}

			return EMOTION_NAMES[intensity, (int) emotion];
		}

	}

}