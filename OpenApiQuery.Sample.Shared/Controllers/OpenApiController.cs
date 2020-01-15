using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenApiQuery.Sample.Data;
using OpenApiQuery.Sample.Models;

namespace OpenApiQuery.Sample.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OpenApiController<T> : Controller where T : class, IEntity
    {
        private readonly BlogDbContext _context;
        private readonly DbSet<T> _records;

        public OpenApiController(BlogDbContext context, DbSet<T> records)
        {
            _context = context;
            _records = records;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(
            OpenApiQueryOptions<T> queryOptions,
            CancellationToken cancellationToken)
        {
            return Ok(await queryOptions.ApplyToAsync(_records, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAsync(
            [FromRoute] int id,
            OpenApiQueryOptions<T> queryOptions,
            CancellationToken cancellationToken)
        {
            var result = await queryOptions.ApplyToSingleAsync(
                _records.Where(u => u.Id == id),
                cancellationToken
            );
            if (result.ResultItem == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync([FromBody] Single<T> item, CancellationToken cancellationToken)
        {
            var value = item.ResultItem;
            await _records.AddAsync(value, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var queryOptions = new OpenApiQueryOptions<T>();
            var result = queryOptions.ApplyToSingle(
                value,
                cancellationToken
            );

            return Created("", result);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchAsync([FromRoute] int id, [FromBody] Delta<T> value, CancellationToken cancellationToken)
        {
            var current = await _records.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (current == null)
            {
                return NotFound();
            }

            value.ApplyPatch(current);

            TryValidateModel(current);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(current);
        }
    }
}
