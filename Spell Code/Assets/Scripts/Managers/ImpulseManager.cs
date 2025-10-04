//using UnityEngine;

//public class ImpulseManager : NonPersistantSingleton<ImpulseManager>
//{
//    public void UpdateImpulse()
//    {
//        PlayerController[] players = GameSessionManager.Instance.playerControllers;

//        foreach (PlayerController player in players)
//        {
//            (ImpulseData[], int[]) impulseData = CharacterDataDictionary.GetImpulseInfo(player.characterName, player.state);

//            if (impulseData.Item1 == null ||
//                impulseData.Item2 == null)
//                return;

//            for (int i = 0; i < impulseData.Item2.Length; i++)
//            {
//                if (impulseData.Item2[i] == player.logicFrame)
//                {
//                    Debug.Log($"Impulse Added!");
//                    AddImpulse(player, impulseData.Item1[i]);
//                }
//            }
//        }
//    }

//    public void UpdateImpulse(PlayerController player)
//    {
//        (ImpulseData[], int[]) impulseData = CharacterDataDictionary.GetImpulseInfo(player.characterName, player.state);

//        if (impulseData.Item1 == null ||
//            impulseData.Item2 == null)
//            return;

//        if (impulseData.Item1.Length != impulseData.Item2.Length)
//            return;

//        for (int i = 0; i < impulseData.Item2.Length; i++)
//        {
//            if (impulseData.Item2[i] == player.logicFrame)
//            {
//                AddImpulse(player, impulseData.Item1[i]);
//            }
//        }
//    }

//    public void AddImpulse(PlayerController player, ImpulseData data)
//    {
//        if (data.resetXVel)
//            player.hSpd = data.xImpulse * (player.facingRight ? 1 : -1);
//        else
//            player.hSpd += data.xImpulse * (player.facingRight ? 1 : -1);

//        if (data.resetYVel)
//            player.vSpd = data.yImpulse;
//        else
//            player.vSpd += data.yImpulse;
//    }
//}
