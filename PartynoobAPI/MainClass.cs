using System;
using BepInEx;
using UnityEngine;
namespace PartynoobAPI
{
    [BepInDependency("mtm101.rulerp.bbplus.baldidevapi")]
    [BepInPlugin("Partynoob.PartynoobAPI","PartynoobAPI", "1.0.0.0")]
    public class PartynoobAPI : BaseUnityPlugin
    {
        public static PartynoobAPI Instance;
        void Awake()
        {
            
            Instance = this;
            
        }
        
    }

    
    abstract public class BaseBaldiMod : BaseUnityPlugin
    {
        public abstract void OnPreload();

        public abstract void OnPostload();

        public
    }
}
