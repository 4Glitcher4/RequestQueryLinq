using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RequestQueryLinq
{
    public class QueryAttribute<T> : ActionFilterAttribute
    {

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            var filter = context.HttpContext.Request.Query["$filter"].ToString();
            var sort = context.HttpContext.Request.Query["$sort"].ToString();
            var take = context.HttpContext.Request.Query["$take"].ToString();
            var skip = context.HttpContext.Request.Query["$skip"].ToString();

            var filters = ParseFilters(filter);
            // Применяем фильтры к результату запроса
            (IQueryable result, int count) = ((IQueryable, int))ApplyFilters(context.Result as OkObjectResult, filters, skip, take, sort);

            // Заменяем результат запроса отфильтрованным результатом
            var executedResult = context.Result as ObjectResult;
            var asd = count;
            var sdf = result as IEnumerable<T>;
            executedResult.Value = new { count, result };

        }

        private dynamic ParseFilters(string filter)
        {
            try
            {
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|in|con)\s*'([\w.-]+)'");
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w.]+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|gt|ls|contains|in)\s*('[^']*'|\(.+?\)|[\w.-]+)");
                var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|ls|contains|in)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                var matches = regex.Matches(filter);

                var filters = matches.Select(match =>
                {
                    var field = match.Groups[1].Value;
                    var operatorType = match.Groups[2].Value;
                    var valueString = string.Empty;

                    object value;
                    if (operatorType == "in")
                    {
                        valueString = match.Groups[3].Value.Trim('\'', '(', ')');
                        value = valueString.Split(new[] { "', '" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(v => ConvertToType(field, v.Trim('\'')))
                                           .ToArray();
                    }
                    else
                    {
                        valueString = match.Groups[3].Value.Trim('\'');
                        value = ConvertToType(field, valueString);  // Преобразуем значение в соответствующий тип
                    }

                    return new FieldFilter
                    {
                        Field = field,
                        Operator = operatorType,
                        Value = value
                    };
                }).ToList();

                return filters;
            }
            catch (Exception)
            {

                throw;
            }
        }

        private object ConvertToType(string fieldName, string value)
        {
            var propertyInfo = GetPropertyInfo(typeof(T), fieldName);
            if (propertyInfo == null) throw new ArgumentException($"Field '{fieldName}' not found on type '{typeof(T).Name}'");

            var propertyType = propertyInfo.PropertyType;

            if (propertyType.IsEnum)
            {
                return Enum.Parse(propertyType, value, true);
            }

            if (propertyType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (propertyType.Namespace == nameof(System))
            {
                return Convert.ChangeType(value, propertyType);
            }

            if (fieldName.Contains("/"))
            {

            }

            return Convert.ChangeType(value, propertyType);
        }

        private PropertyInfo GetPropertyInfo(Type type, string propertyPath)
        {
            PropertyInfo propertyInfo = null;
            foreach (var propertyName in propertyPath.Split('.'))
            {
                propertyInfo = type.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null) return null;
                type = propertyInfo.PropertyType;
            }
            return propertyInfo;
        }

        private (IQueryable result, int count) ApplyFilters(OkObjectResult result, List<FieldFilter> filters, string skip, string take, string sort)
        {
            var queryable = result.Value as IQueryable<T>;
            int count = queryable.Count();
            if (queryable != null)
            {
                // Применяем сортировку
                var results = queryable.ApplyFilter(filters)
                    .ApplySort(sort);

                if (filters.Count > 0)
                {
                    count = results.Count();
                    results = results
                        .Skip(skip)
                        .Take(take);

                    return (results, count);
                }
                else
                {
                    return (results
                                .Skip(skip)
                                .Take(take), count);
                }
            }
            return (queryable, 0);
        }
    }
}
