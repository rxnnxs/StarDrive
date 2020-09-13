#include <quadtree/QuadTree.h>
#include <rpp/timer.h>
#include <rpp/tests.h>


TestImpl(QuadTree)
{
    TestInit(QuadTree)
    {
    }

    static void createTestSpace(tree::QuadTree& tree, int numObjects,
                                std::vector<tree::SpatialObj>& objects)
    {
        float spacing = tree.universeSize() / std::sqrtf((float)numObjects);

        // universe is centered at [0,0], so Root node goes from [-half, +half)
        float half = tree.universeSize() / 2;
        float start = -half + spacing/2;
        float x = start;
        float y = start;

        for (int i = 0; i < numObjects; ++i)
        {
            tree::SpatialObj& o = objects.emplace_back(x, y, 64.0f);
            o.Loyalty = (i % 2) == 0 ? 1 : 2;
            o.Type = 1;
            o.ObjectId = i;

            x += spacing;
            if (x >= half)
            {
                x = start;
                y += spacing;
            }
        }

        tree.updateAll(objects);
    }

    TestCase(search_perf)
    {
        tree::QuadTree tree { 500'000, 512 };
        std::vector<tree::SpatialObj> objects;
        createTestSpace(tree, 10'000, objects);

        const float defaultSensorRange = 30000;
        const int iterations = 200;
        std::vector<int> results(1024, 0);

        rpp::Timer t;
        for (int x = 0; x < iterations; ++x)
        {
            for (const tree::SpatialObj& o : objects)
            {
                tree::SearchOptions opt;
                opt.OriginX = o.CX;
                opt.OriginY = o.CY;
                opt.SearchRadius = defaultSensorRange;
                opt.MaxResults = 1024;
                opt.FilterExcludeObjectId = o.ObjectId;
                opt.FilterExcludeByLoyalty = o.Loyalty;
                int n = tree.findNearby(results.data(), opt);
            }
        }
        double e = t.elapsed_ms();
        printf("QuadTree findNearby elapsed: %.2fms\n", e);
    }
};
