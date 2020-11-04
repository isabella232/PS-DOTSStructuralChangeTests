using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;

#if true

[BurstCompile]
[UpdateBefore(typeof(TransformSystemGroup))]
public class WeaponEquipSystem_HandlerUsingECB : EntityCommandBufferSystem
{
    bool initialized = false;

    public JobHandle commandQueueDependency;

    public struct Command
    {
        public Entity weaponOwner;
        public Weapon currentWeaponRef;
        public int newWeaponIdx;
        public float4x4 localToParent;
    }
    public NativeQueue<Command>.ParallelWriter commandQueueFrontEnd;
    NativeQueue<Command> commandQueue;

    NativeList<Entity> weaponPrefabs;

    NativeList<Entity> weaponToDestroyCache;
    NativeList<Entity> newWeaponInstantiatedCache;

    protected override void OnCreate()
    {
        base.OnCreate();

        commandQueue = new NativeQueue<Command>(Allocator.Persistent);
        commandQueueFrontEnd = commandQueue.AsParallelWriter();

        weaponPrefabs = new NativeList<Entity>(Allocator.Persistent);

        weaponToDestroyCache = new NativeList<Entity>(Allocator.Persistent);
        newWeaponInstantiatedCache = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        newWeaponInstantiatedCache.Dispose();
        weaponToDestroyCache.Dispose();

        weaponPrefabs.Dispose();

        commandQueue.Dispose();

        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        // Some initialization must happen on first update for singleton entity query to work
        if (!initialized)
        {
            initialized = true;

            var weaponDataEntity = GetSingletonEntity<WeaponDataEntry>();
            var weaponDatabase = EntityManager.GetBuffer<WeaponDataEntry>(weaponDataEntity);

            weaponPrefabs.Clear();
            for (int i = 0; i < weaponDatabase.Length; ++i)
            {
                weaponPrefabs.Add(weaponDatabase[i].prefab);
            }
        }

        var commandBuffer = CreateCommandBuffer();

        commandQueueDependency.Complete();

        // Quick way to burst compile processing of command
        var job = new ProcessCommandJob
        {
            commandBuffer = commandBuffer,
            commandQueue = commandQueue,
            weaponToDestroyCache = weaponToDestroyCache,
            newWeaponInstantiatedCache = newWeaponInstantiatedCache,
            weaponPrefabs = weaponPrefabs
        };
        job.Run();

        base.OnUpdate();
    }

    [BurstCompile]
    struct ProcessCommandJob : IJob
    {
        public EntityCommandBuffer commandBuffer;
        public NativeQueue<Command> commandQueue;
        public NativeList<Entity> weaponToDestroyCache;
        public NativeList<Entity> newWeaponInstantiatedCache;
        public NativeList<Entity> weaponPrefabs;

        public void Execute()
        {
            var commandQueueArray = commandQueue.ToArray(Allocator.Temp);
            {
                // Mass destroy
                weaponToDestroyCache.Clear();
                for (int i = 0; i < commandQueueArray.Length; ++i)
                {
                    var command = commandQueueArray[i];
                    if (command.currentWeaponRef.Value != Entity.Null)
                    {
                        weaponToDestroyCache.Add(command.currentWeaponRef.Value);
                    }
                }

                if (weaponToDestroyCache.Length > 0)
                {
                    // To be replaced with batched ECB destroy when API ships
                    {
                        for (int i = 0; i < weaponToDestroyCache.Length; ++i)
                            commandBuffer.DestroyEntity(weaponToDestroyCache[i]);
                    }
                }

                // Instantiate has to 1-1 since mass instantiate does not work with LinkedEntityGroup
                newWeaponInstantiatedCache.Clear();
                for (int i = 0; i < commandQueueArray.Length; ++i)
                {
                    var command = commandQueueArray[i];
                    newWeaponInstantiatedCache.Add(commandBuffer.Instantiate(weaponPrefabs[command.newWeaponIdx]));
                }

                // Mass add component
                {
                    // To be replaced with batched ECB add component when API ships
                    {
                        for (int i = 0; i < newWeaponInstantiatedCache.Length; ++i)
                            commandBuffer.AddComponent<Parent>(newWeaponInstantiatedCache[i]);

                        for (int i = 0; i < newWeaponInstantiatedCache.Length; ++i)
                            commandBuffer.AddComponent<LocalToParent>(newWeaponInstantiatedCache[i]);
                    }
                }

                // There is no mass set component but setting component is way less of a cost than add component
                for (int i = 0; i < newWeaponInstantiatedCache.Length; ++i)
                {
                    var command = commandQueueArray[i];
                    var newWeapon = newWeaponInstantiatedCache[i];

                    commandBuffer.SetComponent(newWeapon, new Parent { Value = command.weaponOwner });
                    commandBuffer.SetComponent(newWeapon, new LocalToParent { Value = command.localToParent });

                    commandBuffer.SetComponent(command.weaponOwner, new Weapon { Value = newWeapon });
                }
            }
            commandQueueArray.Dispose();

            commandQueue.Clear();
        }
    }
}

[UpdateBefore(typeof(WeaponEquipSystem_HandlerUsingECB))]
public class WeaponEquipSystemTest_HandlerUsingECB : SystemBase
{
    int currentWeaponIdx;

    EntityQuery m_WeaponOwner;

    protected override void OnCreate()
    {
        m_WeaponOwner = GetEntityQuery(ComponentType.ReadOnly<Weapon>());
    }

    [BurstCompile]
    struct WeaponEquipJob : IJobChunk
    {
        public int weaponToEquipIdx;

        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;

        [ReadOnly]
        public ComponentTypeHandle<Weapon> weaponRefHandle;

        public NativeQueue<WeaponEquipSystem_HandlerUsingECB.Command>.ParallelWriter commandQueue;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entityChunk = chunk.GetNativeArray(entityTypeHandle);
            var weaponRefChunk = chunk.GetNativeArray(weaponRefHandle);

            for (var i = 0; i < entityChunk.Length; i++)
            {
                var ownerEntity = entityChunk[i];
                var weaponRef = weaponRefChunk[i];

                commandQueue.Enqueue(new WeaponEquipSystem_HandlerUsingECB.Command
                {
                    weaponOwner = ownerEntity,
                    currentWeaponRef = weaponRef,
                    newWeaponIdx = weaponToEquipIdx,
                    localToParent = float4x4.identity
                });
            }
        }
    }

    protected override void OnUpdate()
    {
        ++currentWeaponIdx;
        currentWeaponIdx = currentWeaponIdx % 4;

        var entityTypeHandle = GetEntityTypeHandle();
        var weaponRefHandle = GetComponentTypeHandle<Weapon>(true);

        var weaponEquipSystem = World.GetExistingSystem<WeaponEquipSystem_HandlerUsingECB>();

        var job = new WeaponEquipJob()
        {
            weaponToEquipIdx = currentWeaponIdx,

            entityTypeHandle = entityTypeHandle,
            weaponRefHandle = weaponRefHandle,

            commandQueue = weaponEquipSystem.commandQueueFrontEnd
        };

        Dependency = job.ScheduleSingle(m_WeaponOwner, Dependency);
        weaponEquipSystem.commandQueueDependency = Dependency;
    }
}

#endif
