using System;
using UnityEngine;

public class DataManagerTest : MonoBehaviour
{
    void Start()
    {
        TestConfNPC();
        TestConfItem();
        TestConfInitUnits();
    }

    void TestConfNPC()
    {
        Debug.Log("=== Testing ConfNPC ===");
        
        // 测试获取存在的NPC数据
        var npc1 = DataManager.Get<ConfNPC>("BS001");
        if (npc1 != null)
        {
            Debug.Log($"Found NPC: ID={npc1.ID}, Name={npc1.Name}, HP={npc1.HP}, Attack={npc1.Attack}, Defence={npc1.Defence}");
        }
        else
        {
            Debug.LogError("Failed to load NPC with ID BS001");
        }

        // 测试获取另一个存在的NPC数据
        var npc2 = DataManager.Get<ConfNPC>("BS002");
        if (npc2 != null)
        {
            Debug.Log($"Found NPC: ID={npc2.ID}, Name={npc2.Name}, HP={npc2.HP}, Attack={npc2.Attack}, Defence={npc2.Defence}");
        }
        else
        {
            Debug.LogError("Failed to load NPC with ID BS002");
        }

        // 测试获取不存在的NPC数据
        var npcNotFound = DataManager.Get<ConfNPC>("BS999");
        if (npcNotFound == null)
        {
            Debug.Log("Correctly returned null for non-existent NPC ID BS999");
        }
        else
        {
            Debug.LogError("Should have returned null for non-existent NPC ID BS999");
        }
    }

    void TestConfItem()
    {
        Debug.Log("\n=== Testing ConfItem ===");

        // 测试获取存在的Item数据
        var item1 = DataManager.Get<ConfItem>("WP001");
        if (item1 != null)
        {
            Debug.Log($"Found Item: ID={item1.ID}, Name={item1.Name}, AssetName={item1.AssetName}");
        }
        else
        {
            Debug.LogError("Failed to load Item with ID WP001");
        }

        // 测试获取另一个存在的Item数据
        var item2 = DataManager.Get<ConfItem>("WP002");
        if (item2 != null)
        {
            Debug.Log($"Found Item: ID={item2.ID}, Name={item2.Name}, AssetName={item2.AssetName}");
        }
        else
        {
            Debug.LogError("Failed to load Item with ID WP002");
        }
    }

    void TestConfInitUnits()
    {
        Debug.Log("\n=== Testing ConfInitUnits ===");

        // 测试获取存在的Unit数据
        var unit1 = DataManager.Get<ConfInitUnits>("1");
        if (unit1 != null)
        {
            Debug.Log($"Found Unit: ID={unit1.ID}, Camp={unit1.Camp}, UnitType={unit1.UnitType}, Position={unit1.Position}, Note={unit1.Note}");
        }
        else
        {
            Debug.LogError("Failed to load Unit with ID 1");
        }

        // 测试获取另一个存在的Unit数据
        var unit2 = DataManager.Get<ConfInitUnits>("2");
        if (unit2 != null)
        {
            Debug.Log($"Found Unit: ID={unit2.ID}, Camp={unit2.Camp}, UnitType={unit2.UnitType}, Position={unit2.Position}, Note={unit2.Note}");
        }
        else
        {
            Debug.LogError("Failed to load Unit with ID 2");
        }
    }
}