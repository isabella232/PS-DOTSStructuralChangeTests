using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

#if false

[UpdateBefore(typeof(TransformSystemGroup))]
public class WeaponEquipSystem_HandlerUsingEntityManagerV2 : SystemBase
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

        commandQueueDependency.Complete();

        using (var commandQueueArray = commandQueue.ToArray(Allocator.Temp))
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
                EntityManager.DestroyEntity(weaponToDestroyCache);

            // Instantiate has to 1-1 since mass instantiate does not work with LinkedEntityGroup
            newWeaponInstantiatedCache.Clear();
            for (int i = 0; i < commandQueueArray.Length; ++i)
            {
                var command = commandQueueArray[i];
                newWeaponInstantiatedCache.Add(EntityManager.Instantiate(weaponPrefabs[command.newWeaponIdx]));
            }

            // Mass add component
            EntityManager.AddComponent<Parent>(newWeaponInstantiatedCache);
            EntityManager.AddComponent<LocalToParent>(newWeaponInstantiatedCache);

            // There is no mass set component but setting component is way less of a cost than add component
            for (int i = 0; i < newWeaponInstantiatedCache.Length; ++i)
            {
                var command = commandQueueArray[i];
                var newWeapon = newWeaponInstantiatedCache[i];

                EntityManager.SetComponentData(newWeapon, new Parent { Value = command.weaponOwner });
                EntityManager.SetComponentData(newWeapon, new LocalToParent { Value = command.localToParent });

                EntityManager.SetComponentData(command.weaponOwner, new Weapon { Value = newWeapon });
            }
        }

        commandQueue.Clear();
    }
}

[UpdateBefore(typeof(WeaponEquipSystem_HandlerUsingEntityManagerV2))]
public class WeaponEquipSystemTest_HandlerUsingEntityManagerV2 : SystemBase
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

        public NativeQueue<WeaponEquipSystem_HandlerUsingEntityManagerV2.Command>.ParallelWriter commandQueue;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var entityChunk = chunk.GetNativeArray(entityTypeHandle);
            var weaponRefChunk = chunk.GetNativeArray(weaponRefHandle);

            for (var i = 0; i < entityChunk.Length; i++)
            {
                var ownerEntity = entityChunk[i];
                var weaponRef = weaponRefChunk[i];

                commandQueue.Enqueue(new WeaponEquipSystem_HandlerUsingEntityManagerV2.Command
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

        var weaponEquipSystem = World.GetExistingSystem<WeaponEquipSystem_HandlerUsingEntityManagerV2>();

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
