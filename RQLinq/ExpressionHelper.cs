using System.Linq.Expressions;
using System.Reflection;

namespace RQLinq
{
    public static class ExpressionHelper
    {
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> queryable, string sort)
        {
            if (!string.IsNullOrEmpty(sort))
            {
                var sorts = sort.Split(',')
                    .Select(s => s.Trim().Split(' '))
                    .Select(s => new
                    {
                        Field = s[0],
                        Direction = s[1].ToLower() == "asc" ? nameof(Enumerable.OrderBy) : nameof(Enumerable.OrderByDescending)
                    })
                    .ToList();

                var parameter = Expression.Parameter(typeof(T), "x");
                var orderByExp = queryable.Expression;

                foreach (var sortItem in sorts)
                {
                    var property = Expression.Property(parameter, sortItem.Field);
                    orderByExp = Expression.Call(
                        typeof(Queryable),
                        sortItem.Direction,
                        new[] { typeof(T), property.Type },
                        orderByExp,
                        Expression.Lambda(property, parameter));
                }

                queryable = queryable.Provider.CreateQuery<T>(orderByExp);
                return queryable;
            }
            return queryable;
        }

        //public static int Count<T>(this IQueryable<T> queryable)
        //{
        //    var countExpression = Expression.Call(
        //           typeof(Queryable),
        //           nameof(Enumerable.Count),
        //           new[] { typeof(T) },
        //           queryable.Expression);


        //    var countLambda = Expression.Lambda<Func<int>>(countExpression);
        //    return queryable.Provider.Execute<int>(countExpression);
        //}

        public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> queryable, List<FieldFilter> filters)
        {
            foreach (var filter in filters)
            {
                var field = filter.Field;
                var value = filter.Value;

                // Создаем выражение для фильтрации
                ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
                Expression property = GetPropertyExpression(parameter, field);
                ConstantExpression constant = Expression.Constant(value, value.GetType());
                BinaryExpression comparison;
                LambdaExpression lambda;
                MethodCallExpression methodCallExpression;

                switch (filter.Operator)
                {
                    case "gt":
                        comparison = Expression.GreaterThan(property, constant);
                        break;
                    case "ls":
                        comparison = Expression.LessThan(property, constant);
                        break;
                    case "eq":
                        comparison = Expression.Equal(property, constant);
                        break;
                    case "in":
                        ConstantExpression[] values = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                        NewArrayExpression constantArray = Expression.NewArrayInit(property.Type, values);
                        MethodInfo inMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);

                        MethodCallExpression containsMethodCall = Expression.Call(inMethod, constantArray, property);

                        lambda = Expression.Lambda(containsMethodCall, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    case "contains":
                        if (property.Type == typeof(string))
                        {
                            MethodInfo toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                            MethodCallExpression propertyToLower = Expression.Call(property, toLowerMethod);
                            MethodCallExpression constantToLower = Expression.Call(constant, toLowerMethod);

                            MethodInfo containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                            MethodCallExpression containsMethodExp = Expression.Call(propertyToLower, containsMethod, constantToLower);

                            lambda = Expression.Lambda(containsMethodExp, parameter);

                            methodCallExpression = Expression.Call(
                                typeof(Queryable),
                                nameof(Enumerable.Where),
                                new[] { typeof(T) },
                                queryable.Expression,
                                lambda);

                            queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                            continue;
                        }
                        throw new ArgumentException($"The 'con' operator can only be used with string fields.");
                    default:
                        continue;
                }

                lambda = Expression.Lambda(comparison, parameter);
                methodCallExpression = Expression.Call(
                    typeof(Queryable),
                    nameof(Enumerable.Where),
                    new[] { typeof(T) },
                    queryable.Expression,
                    lambda);

                queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
            }

            return queryable;
        }

        public static IQueryable<T> Skip<T>(this IQueryable<T> queryable, string skip)
        {
            if (!string.IsNullOrEmpty(skip))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Skip),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant(int.Parse(skip))));

            }

            return queryable;
        }

        public static IQueryable<T> Take<T>(this IQueryable<T> queryable, string take)
        {
            if (!string.IsNullOrEmpty(take))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Take),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant(int.Parse(take))));
            }

            return queryable;
        }

        private static Expression GetPropertyExpression(ParameterExpression parameter, string fieldName)
        {
            if (fieldName.Contains('.'))
            {
                // Если название поля содержит точку, это вложенное свойство
                string[] propertyNames = fieldName.Split('.');
                Expression propertyExpression = parameter;

                foreach (var propertyName in propertyNames)
                {
                    propertyExpression = Expression.Property(propertyExpression, propertyName);
                }

                return propertyExpression;
            }
            else
            {
                // Иначе это свойство верхнего уровня
                return Expression.Property(parameter, fieldName);
            }
        }
    }

    public class FieldFilter
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
    }
}
