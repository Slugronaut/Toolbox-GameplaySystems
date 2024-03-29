﻿using UnityEngine;
using Sirenix.OdinInspector;
using Peg.Messaging;
using Peg.MessageDispatcher;

namespace Peg.Game.ConsumableResource
{
    /// <summary>
    /// General-purpose character stat that stores a current and max value as floats.
    /// Useful for things like Health, Stamina, ammo, etc...
    /// </summary>
    [AddComponentMenu("Toolbox/Game/Building Blocks/Entity Resource")]
    public class EntityResource : LocalListenerMonoBehaviour, IEntityResource
    {
        //cached message for re-use
        static readonly EntityResourceChangedEvent Msg = new(null, null, 0);
        static readonly EntityResourceGainedEvent GainedEvent = new(null, null, 0);
        static readonly EntityResourceLostEvent LostEvent = new(null, null, 0);

        [SerializeField]
        [HideInInspector]
        private HashedString _Name = new HashedString("Entity Stat");
        [ShowInInspector]
        [PropertyTooltip("The identifying name of this individual stat. Often used within game logic to identify the purpose of this stat. (e.x. health, mana, ammo, etc...")]
        public HashedString Name { get { return _Name; } set { _Name = value; } }


        /// <summary>
        /// Gets or sets the currenfloat health.
        /// </summary>
        [SerializeField]
        [HideInInspector]
        protected float _Current;
        [ShowInInspector]
        [PropertyTooltip("The current resource amount left.")]
        public float Current
        {
            get { return _Current; }
            set
            {
                //We must be sure we don't try to use the SetCurrent() when not in play-mode in the editor.
                //It will try to create an instance of the message pump and badness will ensue.
                #if UNITY_EDITOR
                if (Application.isPlaying) SetCurrent(null, value);
                else _Current = value;
                #else
                SetCurrent(null, value);
                #endif
            }
        }

        /// <summary>
        /// Gets or sets the current value as a percentage expressed between 0.0 and 1.0
        /// </summary>
        public float CurrentPercent
        {
            get { return _Current / Max; }
            set { Current = value * Max; }
        }

        [BoxGroup("Limits", Order = 1)]
        [Tooltip("If true, resource will always be capped at maximum value. Otheriwse current can exceed max.")]
        public bool EnforceMax = true;

        [SerializeField]
        [HideInInspector]
        protected float _Max;
        [BoxGroup("Limits", Order = 1)]
        [ShowInInspector]
        [ShowIf("EnforceMax")]
        [Indent]
        [PropertyTooltip("The maximum value this stat is meant to have. The current value can always be set higher than this value. It is simply used as a way for game logic to check and enforce such limits.")]
        public float Max
        {
            get { return _Max; }
            set
            {
                //_Max = Mathf.Min(value, _Max);
                if (_Max < _Min) _Max = _Min;
            }
        }

        [BoxGroup("Limits", Order = 1)]
        [Tooltip("If true, resource will always be capped at minimum value. Otheriwse current can exceed max.")]
        public bool EnforceMin = true;

        [SerializeField]
        [HideInInspector]
        protected float _Min;
        [BoxGroup("Limits", Order = 1)]
        [ShowInInspector]
        [ShowIf("EnforceMin")]
        [Indent]
        [PropertyTooltip("The minimum value this stat is meant to have. The current value can always be set higher than this value. It is simply used as a way for game logic to check and enforce such limits.")]
        public float Min
        {
            get { return _Min; }
            set
            {
                //_Min = Mathf.Max(value, _Min);
                if (_Min > _Max) _Min = Max;
            }
        }

        public bool IsDepleted
        {
            get { return (_Max <= 0 || !gameObject.activeInHierarchy || !gameObject.activeSelf); }
        }


        protected virtual void OnEnable()
        {
            DispatchRoot.AddLocalListener<ChangeEntityResourceCmd>(HandleAffect);
            if(EnforceMax && Current > Max) Current = Max;
        }

        protected virtual void OnDisable()
        {
            DispatchRoot.RemoveLocalListener<ChangeEntityResourceCmd>(HandleAffect);
        }

        /// <summary>
        /// Handles in-coming requests to change our current stat value.
        /// </summary>
        /// <param name="message"></param>
        void HandleAffect(ChangeEntityResourceCmd message)
        {
            SetCurrent(message.Agent, _Current - message.Change);
        }

        /// <summary>
        /// Sets this object's current stat value while marking 'agent' as responsible for the change.
        /// 'agent' will also receive a local dispatch of this message.
        /// </summary>
        /// <param name="agent">The entity that caused this change in stat value to occur.</param>
        /// <param name="value">The new current value of the stat.</param>
        public virtual void SetCurrent(GameObject agent, float value, bool suppressEvents = false)
        {
            float diff = _Current - value;
            _Current = value;

            if (EnforceMin && _Current < _Min) Current = Min;
            if (EnforceMax && _Current > _Max) Current = Max;

            if (suppressEvents) return;

            Msg.ChangeValues(agent, this, diff);
            GlobalMessagePump.Instance.PostMessage(Msg);//tell the world
            GlobalMessagePump.Instance.ForwardDispatch(gameObject, Msg);//tell me
            if (diff > 0) GlobalMessagePump.Instance.ForwardDispatch(gameObject, GainedEvent.ChangeValues(agent, this, diff));
            else if(diff < 0) GlobalMessagePump.Instance.ForwardDispatch(gameObject, LostEvent.ChangeValues(agent, this, diff));
            if (agent != null) GlobalMessagePump.Instance.ForwardDispatch(agent, Msg);//tell the one that caused this to happen
            
        }

    }

}
