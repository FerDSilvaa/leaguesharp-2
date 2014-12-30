﻿using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

namespace LeBlanc
{
    internal class Program
    {
        public static Menu Menu;
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static Orbwalking.Orbwalker Orbwalker;
        public static Obj_AI_Hero Target;
        public static Obj_AI_Hero Player = ObjectManager.Player;
        public static bool LCombo;
        public static Obj_AI_Base Clone;
        public static SpellDataInst Ignite;

        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.ChampionName.ToLower() != "leblanc")
            {
                return;
            }

            Menu = new Menu("LeBlanc The Schemer", "LeBlanc", true);

            Menu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            Orbwalker = new Orbwalking.Orbwalker(Menu.SubMenu("Orbwalker"));

            var ts = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(ts);
            Menu.AddSubMenu(ts);

            var combo = new Menu("Combo Settings", "Combo");

            var gapclose = combo.AddSubMenu(new Menu("GapClose", "Gap Close Combo"));
            gapclose.AddItem(new MenuItem("Spacer", "This doesn't work yet"));
            gapclose.AddItem(new MenuItem("GapCloseEnabled", "Use GapClose Combo").SetValue(true));
            //replace with damage calcs
            gapclose.AddItem(new MenuItem("TargetHP", "Min Target HP %").SetValue(new Slider(40)));
            gapclose.AddItem(new MenuItem("PlayerHP", "Min Player HP %").SetValue(new Slider(40)));

            combo.AddItem(new MenuItem("ComboQ", "Use Q").SetValue(true));
            combo.AddItem(new MenuItem("ComboW", "Use W").SetValue(true));
            combo.AddItem(new MenuItem("ComboE", "Use E").SetValue(true));
            combo.AddItem(new MenuItem("ComboR", "Use R").SetValue(true));
            combo.AddItem(new MenuItem("ComboQRange", "Only Combo in Q Range").SetValue(true));
            combo.AddItem(
                new MenuItem("ComboUltMode", "Ult Mode").SetValue(new StringList(new[] { SpellSlot.Q.ToString() })));
            // SpellSlot.W.ToString(), SpellSlot.E.ToString() })));
            combo.AddItem(new MenuItem("ComboKey", "Combo Key").SetValue(new KeyBind(32, KeyBindType.Press)));
            Menu.AddSubMenu(combo);

            var harass = Menu.AddSubMenu(new Menu("Harass Settings", "Harass"));
            harass.AddItem(new MenuItem("HarassQ", "Use Q").SetValue(true));
            harass.AddItem(new MenuItem("HarassW", "Use W").SetValue(true));
            harass.AddItem(new MenuItem("HarassE", "Use E").SetValue(true));
            harass.AddItem(new MenuItem("HarassR", "Use R").SetValue(true));
            harass.AddItem(
                new MenuItem("SecondW", "Second W Setting").SetValue(
                    new StringList(new[] { "Manual", "Auto", "After E" })));
            harass.AddItem(new MenuItem("Harass", "Harass Key").SetValue(new KeyBind((byte) 'C', KeyBindType.Press)));

            var laneclear = Menu.AddSubMenu(new Menu("Farm Settings", "LaneClear"));
            laneclear.AddItem(new MenuItem("LaneClearQ", "Use Q").SetValue(true));
            laneclear.AddItem(new MenuItem("LaneClearQPercent", "Minimum Q Mana Percent").SetValue(new Slider(30)));
            laneclear.AddItem(
                new MenuItem("LaneClearActive", "LaneClear").SetValue(new KeyBind((byte) 'V', KeyBindType.Press)));

            var clone = Menu.AddSubMenu(new Menu("Clone Settings", "Clone"));
            clone.AddItem(new MenuItem("Enabled", "Enabled").SetValue(true));
            clone.AddItem(
                new MenuItem("Mode", "Mode").SetValue(
                    new StringList(new[] { "To Player", "To Target", "Away from Player" })));

            var misc = Menu.AddSubMenu(new Menu("Misc Settings", "Misc"));
            misc.AddItem(new MenuItem("Interrupt", "Interrupt Spells").SetValue(true));
            misc.AddItem(new MenuItem("MiscItems", "Use Items (DFG)").SetValue(true));
            misc.AddItem(new MenuItem("MiscW2", "Use Second W").SetValue(true));
            misc.AddItem(new MenuItem("MiscW2HP", "HP% to Use Second W").SetValue(new Slider(20)));

            Menu.AddToMainMenu();

            Ignite = Player.Spellbook.GetSpell(Player.GetSpellSlot("summonerdot"));

            Q = new Spell(SpellSlot.Q, 700);
            Q.SetTargetted(.401f, 2000);

            W = new Spell(SpellSlot.W, 600);
            W.SetSkillshot(.5f, 100, 2000, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 970);
            E.SetSkillshot(.366f, 70, 1600, true, SkillshotType.SkillshotLine);

            R = new Spell(SpellSlot.R);

            Game.PrintChat(
                "<b><font color =\"#FFFFFF\">LeBlanc the Schemer by </font><font color=\"#5C00A3\">Trees</font><font color =\"#FFFFFF\"> loaded!</font></b>");

            Game.OnGameUpdate += Game_OnGameUpdate;
            GameObject.OnCreate += Obj_AI_Base_OnCreate;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
            //Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            // Console.WriteLine(Player.Spellbook.GetSpell(SpellSlot.R).Name);
        }

        private static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Menu.Item("Interrupt").GetValue<bool>() || unit == null || !unit.IsValid ||
                !unit.IsValidTarget(E.Range) || spell.DangerLevel < InterruptableDangerLevel.High)
            {
                return;
            }

            if (!E.IsReady())
            {
                return;
            }

            E.CastIfHitchanceEquals(unit, HitChance.Medium);

            Utility.DelayAction.Add(
                (int) E.Delay * 1000 + 100, () =>
                {
                    if (R.IsReady() && UltType() == SpellSlot.E)
                    {
                        SetRMode(SpellSlot.E);
                        R.CastIfHitchanceEquals(unit, HitChance.Medium);
                    }
                });
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender == null || !sender.IsValid || !sender.IsMe)
            {
                return;
            }

            if (Target == null || !Target.IsValid || !Menu.Item("ComboKey").GetValue<bool>() ||
                args.SData.Name != Q.Instance.Name) // || !LCombo)
            {
                Console.WriteLine("NO TARG");
                return;
            }

            Console.WriteLine("CAST ULT");
            SetRMode(SpellSlot.Q);
            R.CastOnUnit(Target);
            /*  if (args.Slot != SpellSlot.W || UltType() == SpellSlot.R)
            {
                Console.WriteLine("RETURN");
                return;
            }*/

            /*  Utility.DelayAction.Add(
                160, () =>
                {
                    Console.WriteLine("ULT");
                    Player.Spellbook.CastSpell(SpellSlot.R, Target.Position);
                    LCombo = false;
                });

           */
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
            {
                return;
            }

            KSIgnite();
            CloneLogic();
            SecondWLogic();

            Target = TargetSelector.GetTarget(2000, TargetSelector.DamageType.Magical);

            if (Target == null || !Target.IsValid || !Target.IsValidTarget(2000))
            {
                return;
            }

            /*   if (LCombo && R.IsReady() && UltType() == SpellSlot.W)
            {
               Console.WriteLine("L23");
                if (Player.HealthPercentage() < 20 && IsSecondW())
                {
                    Player.Spellbook.CastSpell(SpellSlot.W);
                    return;
                }
                ItemId.Deathfire_Grasp.Cast(Target);
              //  Player.Spellbook.CastSpell(SpellSlot.R, Target.Position);
                Q.CastOnUnit(Target);
                E.Cast(Target);
                LCombo = false;
            }
        */
            if (Menu.SubMenu("Combo").Item("ComboKey").GetValue<KeyBind>().Active)
            {
                Comboes();
                return;
            }


            if (Menu.Item("Harass").GetValue<KeyBind>().Active)
            {
                Harass();
                return;
            }


            if (Menu.Item("LaneClear").GetValue<KeyBind>().Active)
            {
                LaneClear();
            }
        }

        private static void Obj_AI_Base_OnCreate(GameObject sender, EventArgs args)
        {
            if (sender.IsValid && sender.Name.Equals(Player.Name))
            {
                Clone = sender as Obj_AI_Base;
            }
        }

        private static void Comboes()
        {
            //Menu.Item("GapCloseEnabled").GetValue<bool>() &&
            /*if (LCombo)
            {
                return;
            }
            */

            if (Player.Distance(Target) < W.Range * 2)
            {
                Target = TargetSelector.GetTarget(800, TargetSelector.DamageType.Magical);
                Combo();
            }
            // WCombo();
        }

        private static void Combo()
        {
            if (Target == null || !Target.IsValid ||
                !Target.IsValidTarget(Menu.Item("ComboQRange").GetValue<bool>() ? Q.Range : float.MaxValue))
            {
                return;
            }

            var castQ = Menu.Item("ComboQ").GetValue<bool>();
            var castW = Menu.Item("ComboW").GetValue<bool>();
            var castE = Menu.Item("ComboE").GetValue<bool>();
            var castR = Menu.Item("ComboR").GetValue<bool>();
            var castItems = Menu.Item("MiscItems").GetValue<bool>();

            if (castItems && Player.Distance(Target) <= 750 && Q.IsReady() && W.IsReady() && E.IsReady() && R.IsReady())
            {
                ItemId.Deathfire_Grasp.Cast(Target);
                ItemId.Blackfire_Torch.Cast(Target);
            }

            if (castW && W.CanCast(Target) && IsFirstW() && Player.HealthPercentage() >= 20)
            {
                W.RandomizeCast(Target.Position);
            }

            if (castQ && Q.CanCast(Target))
            {
                Q.CastOnUnit(Target);
                return;
            }

            if (castR && R.IsReady() && UltType() == SpellSlot.Q)
            {
                Player.Spellbook.CastSpell(SpellSlot.R, Target);
            }

            if (castE && E.IsReady() && E.InRange(Target, 800))
            {
                E.CastIfHitchanceEquals(Target, HitChance.Medium);
            }
        }

        private static void WCombo()
        {
            if (!W.IsReady() || !IsFirstW() || !R.IsReady() || Target == null || !Target.IsValid ||
                Target.Distance(Player.Position) < W.Range * 2 - 100 ||
                Target.HealthPercentage() < Menu.Item("TargetHP").GetValue<Slider>().Value ||
                Player.HealthPercentage() < Menu.Item("PlayerHP").GetValue<Slider>().Value)
            {
                return;
            }

            Console.WriteLine("LCOMBO");
            LCombo = true;
            var pos = Player.ServerPosition.To2D().Extend(Target.ServerPosition.To2D(), W.Range);
            W.Cast(pos);
            /* Utility.DelayAction.Add(150, () =>
            {
                if (Player.Distance(Target) < 970)
                {
                    ItemId.Blackfire_Torch.Cast(Target);
                }
                if (UltType() == SpellSlot.W)
                {
                    Player.Spellbook.CastSpell(SpellSlot.R, Target.ServerPosition);
                }
                LCombo = false;
            });
        
            */
        }

        private static void Harass()
        {
            var castQ = Menu.Item("HarassQ").GetValue<bool>();
            var castW = Menu.Item("HarassW").GetValue<bool>();
            var castE = Menu.Item("HarassE").GetValue<bool>();

            if (castQ && Q.CanCast(Target))
            {
                Q.Cast(Target);
            }

            if (castE && E.CanCast(Target))
            {
                E.Cast(Target);
            }

            if (!castW || !W.IsReady() || Player.HealthPercentage() < 20)
            {
                return;
            }

            if (IsFirstW() && W.InRange(Target))
            {
                W.Cast(Target);
            }

            else if (IsSecondW())
            {
                switch (GetWMode())
                {
                    case 1:
                        Player.Spellbook.CastSpell(SpellSlot.W);
                        break;
                    case 2:
                        foreach (var b in
                            Target.Buffs.Where(buff => buff.Name.ToLower().Contains("leblancsoulshackle")))
                        {
                            Player.Spellbook.CastSpell(SpellSlot.W);
                        }
                        break;
                }
            }
        }

        private static void LaneClear()
        {
            if (!Q.IsReady() || !Menu.SubMenu("LaneClear").Item("LaneClearQ").GetValue<bool>() ||
                Player.ManaPercentage() < Menu.SubMenu("LaneClear").Item("LaneClearQPercent").GetValue<Slider>().Value)
            {
                return;
            }

            var unit =
                ObjectManager.Get<Obj_AI_Minion>()
                    .FirstOrDefault(
                        minion =>
                            minion.IsValidTarget(Q.Range) &&
                            minion.Health < Player.GetDamageSpell(minion, SpellSlot.Q).CalculatedDamage);
            if (unit != null && unit.IsValid)
            {
                Q.CastOnUnit(unit);
            }
        }

        private static bool IsFirstW()
        {
            return W.Instance.Name == "LeblancSlide";
        }

        private static bool IsSecondW()
        {
            return W.Instance.Name == "leblancslidereturn";
        }

        private static int GetWMode()
        {
            return Menu.SubMenu("Harass").Item("SecondW").GetValue<StringList>().SelectedIndex;
        }

        private static SpellSlot UltType()
        {
            if (R.Instance.Name == null)
            {
                return SpellSlot.R;
            }
            switch (R.Instance.Name)
            {
                //leblancslidereturnm
                case "LeblancChaosOrbM":
                    return SpellSlot.Q;
                case "LeblancSlideM":
                    return SpellSlot.W;
                case "LeblancSoulShackleM":
                    return SpellSlot.E;
                default:
                    return SpellSlot.R;
            }
        }

        private static void SetRMode(SpellSlot slot)
        {
            switch (slot)
            {
                case SpellSlot.Q:
                    R.Range = 700;
                    R.SetTargetted(.401f, 2000);
                    return;
                case SpellSlot.W:
                    R = new Spell(SpellSlot.R, 600);
                    R.SetSkillshot(.5f, 100, 2000, false, SkillshotType.SkillshotCircle);
                    return;
                case SpellSlot.E:
                    R = new Spell(SpellSlot.R, 970);
                    R.SetSkillshot(.366f, 70, 1600, true, SkillshotType.SkillshotLine);
                    return;
            }
        }

        private static void SecondWLogic()
        {
            if (Menu.Item("MiscW2").GetValue<bool>() &&
                Player.HealthPercentage() <= Menu.Item("MiscW2HP").GetValue<Slider>().Value && W.IsReady() &&
                IsSecondW() && !Q.IsReady() && !E.IsReady())
            {
                Player.Spellbook.CastSpell(SpellSlot.W);
            }
        }

        private static void CloneLogic()
        {
            if (Clone == null || !Clone.IsValid || !Menu.SubMenu("Clone").Item("Enabled").GetValue<bool>())
            {
                return;
            }

            var mode = Menu.SubMenu("Clone").Item("Mode").GetValue<StringList>().SelectedIndex;

            switch (mode)
            {
                case 0: // toward player
                    var pos = Player.ServerPosition;
                    if (Player.GetWaypoints().Count > 1)
                    {
                        pos = Player.GetWaypoints()[1].To3D();
                    }
                    Utility.DelayAction.Add(100, () => { Clone.IssueOrder(GameObjectOrder.MovePet, pos); });
                    break;
                case 1: //toward target
                    if (Clone.CanAttack && !Clone.IsWindingUp) // && !Clone.IsAutoAttacking)
                    {
                        Clone.IssueOrder(GameObjectOrder.AutoAttackPet, Target);
                    }
                    break;
                case 2: //away from player
                    Clone.IssueOrder(GameObjectOrder.MovePet, Player.Position.Extend(Clone.Position, 200));
                    break;
            }
        }

        private static void KSIgnite()
        {
            var unit =
                ObjectManager.Get<Obj_AI_Hero>()
                    .FirstOrDefault(
                        obj =>
                            obj.IsValidTarget(600) &&
                            obj.Health < Player.GetSummonerSpellDamage(obj, Damage.SummonerSpell.Ignite));
            if (unit != null && unit.IsValid)
            {
                Player.Spellbook.CastSpell(Ignite.Slot, unit);
            }
        }
    }
}