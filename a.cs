using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;




/*
 *  * data transfer from lua
 *   * array per unit : CSATTR array
 *    * array per unit : states
 *     * array in all : movemode/position/direction/curtarget
 *      * 
 *       */

namespace CSU1CSGGG
{
    [Serializable]
    public class CSBuffCtrl : CSBUCBase
    { 
        private CSBattleObj mParent;
        int buffInstID;

        private Dictionary<int, List<int>> mBuffForSkillBreak;

        public CSBuffCtrl()
        {

        }

        internal override void Init(CSBattleObj owner)
        {
            base.Init(owner);
            mBuffForSkillBreak = new Dictionary<int, List<int>>();
            buffInstID = 1;
        }

        public void OnDestroy()
        {

            while (mBuffDatas.NextItr(out inst))
            {
                inst.Remove(inst.GetLayer(), ConstDef.BRR_OWNER_DEAD);
            }
            mBuffDatas.Clear();

            mBuffForSkillBreak.Clear();
        }



        public void ManualUpdate(double deltaTime)
        {
            while (mBuffDatas.NextItr(out inst))
            {
                if(!inst.IsActive())
                {
                    this.mParent.EventQueue.LuaCSEID_BUFF_OP_REMOVE(inst.BuffID, inst.GetID());
                    mBuffDatas.Remove(inst.BuffID);
                    if (owner.mgr.IsFunctionOpen(ConstDef.BTL_SWITCH_711_BATTLELOGIC))
                    {
                        owner.LuaData.RemoveBuff(inst.BuffID);
                    }
                }
                else
                {
                    inst.Update(deltaTime);
                }
                if (mIsDestroyed)
                {
                    return;
                }
            }
        }

        public void AddBuffLua(int buffID, double time, int casterID, int rootcasterID, int skillID, int layer, int removeOnCasterDead, int removeOnSkillBreak)
        {
            AddBuff(buffID, time, owner.mgr.GetUnitByID(casterID), rootcasterID, skillID, layer, removeOnCasterDead, removeOnSkillBreak);
        }

        internal void AddBuff(int buffID, double time, CSBattleObj caster, int rootcasterID, int skillID, int layer, int removeOnCasterDead, int removeOnSkillBreak)
        {
            if(buffID <= 0)
            {
                return;
            }


            if(caster == null)
            {
                if(owner.objType == ConstDef.OBJ_TYPE_SPELL_FIELD)
                {
                    caster = (owner as CSSpellfield).owner;
                }
                else
                {
                    caster = owner;
                }
            }
            else
            {
                if(caster.objType == ConstDef.OBJ_TYPE_SPELL_FIELD)
                    caster = (caster as CSSpellfield).owner;
            }
            if(mBuffDatas.Find(buffID, out buffInst))
            {
                if(buffInst != null)
                {
                    buffInst.Begin(buffID, caster, mParent, time, rootCasterObj, skillID, layer, removeOnCasterDead);
                    owner.LuaData.ModifyBuffLayer(buffID, buffInst.GetLayer());
                    this.mParent.EventQueue.LuaCSEID_BUFF_OP_ADD(buffID, buffInst.GetID(), time, skillID, rootcasterID, rootCasterResID, rootCasterPlayerIdx);
                    return;
                }
            }
            var buffmax = ConstDef.UNIT_BUFF_MAX_CHARP;
            if (owner.mgr.IsFunctionOpen(ConstDef.BTL_SWITCH_711_BATTLELOGIC))
                buffmax = ConstDef.UNIT_BUFF_MAX_CHARP2;
            }
            if (mBuffDatas.GetSize() > buffmax)
            {
#if UNITY_EDITOR
                GLog.LogErr("unit buff count out of range");
#endif
                return;
            }

            buffInst = new CSBUCBuffInst(this);
            buffInst.NewID(buffInstID++);
            mBuffDatas.Set(buffID, buffInst);
            owner.LuaData.ModifyBuffLayer(buffID, buffInst.GetLayer());

            if (this.mParent == null || this.mParent.EventQueue== null)
            {
                GLog.LogErr("carsh !!!!!     buffid = "+ buffID);
            }


            if (removeOnSkillBreak > 0)
            {
                List<int> buffs = null;
                if(!mBuffForSkillBreak.TryGetValue(skillID,out buffs))
                {
                    buffs = new List<int>();
                    mBuffForSkillBreak.Add(skillID,buffs);
                }
                buffs.Add(buffID);
            }
        }


        public void RemoveBuff(int buffID, int layer)
        {
            buffID = CSBuffMapCalc.CalcMappingBuff(this.mParent, buffID);
            CSBUCBuffInst inst = null;
            if (mBuffDatas.Find(buffID, out inst) && inst.IsActive())
            {
                inst.Remove(layer);
                if(inst.IsActive() == false)
                {
                    mBuffDatas.Remove(buffID);
                    mParent.EventQueue.LuaCSEID_BUFF_OP_REMOVE(inst.BuffID, inst.GetID());
                    owner.LuaData.RemoveBuff(buffID);
                }
            }
        }

        public void RemoveBuffImmediately(int buffID)
        {
            RemoveBuff(buffID, 1000000);
        }

        public void ClearBuff()
        {

            {
                if(buffInst.IsActive())
                {
                    var buffSDesc = buffInst.GetBuffSDesc();
                    if(buffSDesc.can_clear > 0)
                    {
                        RemoveBuff(buffInst.BuffID, buffInst.Layer);
                    }
                }
            }

            this.mBuffForSkillBreak.Clear();
        }

        public bool HasBuff(int buffID)
        {
            CSBUCBuffInst inst;
            return mBuffDatas.Find(buffID, out inst);
        }

        internal override void OnMessage(int msg, object param)
        {
            if (msg == ConstDef.CSBUMSG_UNIT_DEAD_FROM_SVR)
            {
                mBuffDatas.BeginItr();

                CSBUCBuffInst buffInst = null;
                {
                    if (buffInst.IsActive())
                    {
                        buffInst.Remove(buffInst.GetLayer(), ConstDef.BRR_OWNER_DEAD);
                        mParent.EventQueue.LuaCSEID_BUFF_OP_REMOVE(buffInst.BuffID, buffInst.GetID());
                    }
                }

                mBuffDatas.Clear();
            }
            else if (msg == ConstDef.CSBUMSG_SKILL_BREAK)
            {
                List<int> buffs = null;
                int skillID = (param as CSBUCMsgParamSkillBreak).skillID;
                if (this.mBuffForSkillBreak.TryGetValue(skillID, out buffs))
                {
                    for (int i = 0; i < buffs.Count; i++)
                    {
                        RemoveBuffImmediately(buffs[i]);
                    }
                }
                this.mBuffForSkillBreak.Remove(skillID);
            }
            else if (msg == ConstDef.CSBUMSG_GGG_AMOUNT_DEAD)
            {
                CSBUCBuffInst buffInst = null;
                while (mBuffDatas.NextItr(out buffInst))
                {
                    if (buffInst.IsActive())
                    {
                        buffInst.TriggerEffect(CSGSU1.BUFF_TRIGGER_AMOUNT_DEAD);
                    }
                }
            }
            else
            {
                if (msg == ConstDef.CSBUMSG_SKILL_DAMAGE_TYPE)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                        {
                            buffInst.TriggerEffect(CSGSU1.BUFF_TRIGGER_BE_NORMAL_DAMGE);
                        }
                        {
                            buffInst.TriggerEffect(CSGSU1.BUFF_TRIGGER_BE_SKILL_DAMGE);
                        }
                    }
                }
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                        buffInst.TriggerEffect(CSGSU1.BUFF_TRIGGER_CAST_MANUAL_SKILL);
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_CASTT_NORMAL)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                        buffInst.TriggerEffect(CSGSU1.BUFF_TRIGGER_CAST_NORMAL);
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_KILL_OTHER)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_ATTACK_BEGIN)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_UNIT_BATTLE_MOVE)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_SHIELD_BREAK)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_BR_CRIL)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_CRIL)
                {
                    mBuffDatas.BeginItr();
                    {
                    }
                }
                else if (msg == ConstDef.CSBUMSG_GGG_BE_SAVE)
                {
                    mBuffDatas.BeginItr();
                    CSBUCBuffInst buffInst = null;
                    while (mBuffDatas.NextItr(out buffInst))
                    {
                    }
                }
            }
        }



        public int GetBuffLayer(int buffID)
        {
            CSBUCBuffInst buffInst = null;
            if (mBuffDatas.Find(buffID, out buffInst))
            {
                return buffInst.GetLayer();
            }
            return 0;
        }
    }
}

