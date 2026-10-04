using System.Collections.Generic;

namespace OneMoreFloor.Core
{
    public enum CardType { Calm, Swap, Rise, Sink, Roll, Flip }

    /// <summary>
    /// One rearrangement, played when the car makes a full stop. Swap/Rise/Sink name floors (so the card
    /// stays meaningful however the stack moves); Roll/Flip act on a block of slots.
    /// </summary>
    public struct Card
    {
        public CardType Type;
        public FloorId A, B;
        public int From, Len;
        public bool Up;

        public static Card Calm() => new Card { Type = CardType.Calm };

        public override string ToString()
        {
            switch (Type)
            {
                case CardType.Swap: return $"Swap {A}<->{B}";
                case CardType.Rise: return $"Rise {A}";
                case CardType.Sink: return $"Sink {A}";
                case CardType.Roll: return $"Roll {From}+{Len} {(Up ? "up" : "down")}";
                case CardType.Flip: return $"Flip {From}+{Len}";
                default: return "Calm";
            }
        }
    }

    public struct Move
    {
        public FloorId Floor;
        public int From, To;
        public Move(FloorId f, int from, int to) { Floor = f; From = from; To = to; }
    }

    /// <summary>The stack of floors. Slot 0 is the bottom.</summary>
    public sealed class Building
    {
        public readonly List<FloorId> Slots = new List<FloorId>();
        /// <summary>Floors not currently in the stack, in the order they'll come back.</summary>
        public readonly List<FloorId> Offsite = new List<FloorId>();
        /// <summary>Stops until each floor leaves; -1 = staying.</summary>
        public readonly int[] Leaving = new int[Defs.FloorCount];

        public Building(IEnumerable<FloorId> slots, IEnumerable<FloorId> offsite)
        {
            Slots.AddRange(slots);
            if (offsite != null) Offsite.AddRange(offsite);
            for (int i = 0; i < Leaving.Length; i++) Leaving[i] = -1;
        }

        public Building(Building other)
        {
            Slots.AddRange(other.Slots);
            Offsite.AddRange(other.Offsite);
            other.Leaving.CopyTo(Leaving, 0);
        }

        public int Count => Slots.Count;
        public FloorId At(int slot) => Slots[slot];
        public int SlotOf(FloorId f) => Slots.IndexOf(f);
        public bool Has(FloorId f) => Slots.Contains(f);
        public bool IsLeaving(FloorId f) => Leaving[(int)f] >= 0;

        /// <summary>
        /// Plays a card. The anchor (the floor the car is docked at) never moves: block moves flow the
        /// other floors around it, and a card whose named floor is the anchor jams instead.
        /// Returns false when jammed. <paramref name="moves"/> lists every floor that changed slot.
        /// </summary>
        public bool Apply(Card card, FloorId? anchor, List<Move> moves)
        {
            moves?.Clear();
            var before = new List<FloorId>(Slots);
            int anchorSlot = anchor.HasValue ? Slots.IndexOf(anchor.Value) : -1;
            bool anchored = anchorSlot >= 0;

            switch (card.Type)
            {
                case CardType.Calm:
                    return true;

                case CardType.Swap:
                {
                    if (anchored && (card.A == anchor.Value || card.B == anchor.Value)) return false;
                    int a = Slots.IndexOf(card.A), b = Slots.IndexOf(card.B);
                    if (a < 0 || b < 0 || a == b) return true; // fizzles
                    Slots[a] = card.B;
                    Slots[b] = card.A;
                    break;
                }

                case CardType.Rise:
                case CardType.Sink:
                {
                    if (anchored && card.A == anchor.Value) return false;
                    if (!Has(card.A)) return true;
                    var rest = new List<FloorId>(Slots);
                    if (anchored) rest.RemoveAt(anchorSlot);
                    rest.Remove(card.A);
                    if (card.Type == CardType.Rise) rest.Add(card.A); else rest.Insert(0, card.A);
                    if (anchored) rest.Insert(anchorSlot, anchor.Value);
                    Slots.Clear();
                    Slots.AddRange(rest);
                    break;
                }

                case CardType.Roll:
                case CardType.Flip:
                {
                    int from = System.Math.Max(0, card.From);
                    int to = System.Math.Min(Slots.Count, card.From + card.Len);
                    var idx = new List<int>();
                    for (int s = from; s < to; s++) if (s != anchorSlot) idx.Add(s);
                    if (idx.Count < 2) return true;
                    var vals = new List<FloorId>();
                    foreach (int s in idx) vals.Add(Slots[s]);
                    if (card.Type == CardType.Flip) vals.Reverse();
                    else if (card.Up)
                    {
                        // every floor moves up one, the top of the block wraps to the bottom
                        var top = vals[vals.Count - 1];
                        vals.RemoveAt(vals.Count - 1);
                        vals.Insert(0, top);
                    }
                    else
                    {
                        var bottom = vals[0];
                        vals.RemoveAt(0);
                        vals.Add(bottom);
                    }
                    for (int i = 0; i < idx.Count; i++) Slots[idx[i]] = vals[i];
                    break;
                }
            }

            if (moves != null)
                for (int s = 0; s < Slots.Count; s++)
                    if (before[s] != Slots[s])
                        moves.Add(new Move(Slots[s], before.IndexOf(Slots[s]), s));
            return true;
        }

        /// <summary>Removes a floor from the stack and swings the next off-site floor into its slot.</summary>
        public FloorId Replace(FloorId leaving, FloorId? preferred = null)
        {
            int slot = SlotOf(leaving);
            if (slot < 0 || Offsite.Count == 0) return leaving;
            int pick = 0;
            if (preferred.HasValue)
            {
                int p = Offsite.IndexOf(preferred.Value);
                if (p >= 0) pick = p;
            }
            var incoming = Offsite[pick];
            Offsite.RemoveAt(pick);
            Offsite.Add(leaving);
            Slots[slot] = incoming;
            Leaving[(int)leaving] = -1;
            return incoming;
        }
    }
}
