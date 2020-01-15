using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenApiQuery.Utils;

namespace OpenApiQuery
{
    public abstract class OpenApiQueryOptions
    {
        public SelectExpandQueryOption SelectExpand { get; }

        public FilterQueryOption Filter { get; }

        public OrderByQueryOption OrderBy { get; }

        public SkipQueryOption Skip { get; }

        public TopQueryOption Top { get; }

        public CountQueryOption Count { get; }

        public Type ElementType { get; }

        public bool NeedsPolymorphism { get; set; }
        internal HttpContext HttpContext { get; set; }
        internal ModelStateDictionary ModelState { get; set; }

        protected OpenApiQueryOptions(Type elementType)
        {
            ElementType = elementType;
            SelectExpand = new SelectExpandQueryOption(ElementType);
            Filter = new FilterQueryOption(ElementType);
            OrderBy = new OrderByQueryOption(ElementType);
            Skip = new SkipQueryOption();
            Top = new TopQueryOption();
            Count = new CountQueryOption();
        }

        public void Initialize(HttpContext httpContext, ModelStateDictionary modelState)
        {
            HttpContext = httpContext;
            ModelState = modelState;
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<OpenApiQueryOptions>>();
            SelectExpand.Initialize(httpContext, logger, ModelState);
            Filter.Initialize(httpContext, logger, ModelState);
            OrderBy.Initialize(httpContext, logger, ModelState);
            Skip.Initialize(httpContext, ModelState);
            Top.Initialize(httpContext, ModelState);
            Count.Initialize(httpContext, ModelState);
        }
    }

    [OpenApiQueryParameterBindingAttribute]
    public class OpenApiQueryOptions<T> : OpenApiQueryOptions
    {
        public OpenApiQueryOptions()
            : base(typeof(T))
        {
        }

        public async Task<Multiple<T>> ApplyToAsync(
            IQueryable<T> queryable,
            CancellationToken cancellationToken)
        {
            // The order of applying the items to the queryable is important

            // 1. include all related items for further query options
            queryable = SelectExpand.ApplyTo(queryable);
            // 2. sort to have the correct order for filtering and limiting
            queryable = OrderBy.ApplyTo(queryable);
            // 3. filter the items according to the user input
            queryable = Filter.ApplyTo(queryable);

            long? count = null;
            if (Count.Value == true)
            {
                count = await queryable.LongCountAsync(cancellationToken);
            }

            // 4. apply paging on the sorted and filtered result.
            queryable = Skip.ApplyTo(queryable);
            queryable = Top.ApplyTo(queryable);

            var result = await queryable.ToArrayAsync(cancellationToken);

            return new Multiple<T>(this, result, count);
        }

        public async Task<Single<T>> ApplyToSingleAsync(
            IQueryable<T> queryable,
            CancellationToken cancellationToken)
        {
            queryable = SelectExpand.ApplyTo(queryable);

            var result = await queryable.SingleOrDefaultAsync(cancellationToken);

            return new Single<T>(this, result);
        }

        public Single<T> ApplyToSingle(
            T item,
            CancellationToken cancellationToken)
        {
            return new Single<T>(this, item);
        }
    }
}
