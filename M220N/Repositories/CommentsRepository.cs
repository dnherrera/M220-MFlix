﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using M220N.Models;
using M220N.Models.Projections;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace M220N.Repositories
{
    public class CommentsRepository
    {
        private readonly IMongoCollection<Comment> _commentsCollection;
        private readonly MoviesRepository _moviesRepository;

        public CommentsRepository(IMongoClient mongoClient)
        {
            var camelCaseConvention = new ConventionPack {new CamelCaseElementNameConvention()};
            ConventionRegistry.Register("CamelCase", camelCaseConvention, type => true);

            _commentsCollection = mongoClient.GetDatabase("sample_mflix").GetCollection<Comment>("comments");
            _moviesRepository = new MoviesRepository(mongoClient);
        }

        /// <summary>
        ///     Adds a comment.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="movieId"></param>
        /// <param name="comment"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The Movie associated with the comment.</returns>
        public async Task<Movie> AddCommentAsync(User user, ObjectId movieId, string comment,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var newComment = new Comment
                {
                    Date = DateTime.UtcNow,
                    Text = comment,
                    Name = user.Name,
                    Email = user.Email,
                    MovieId = movieId
                };

                // Implement InsertOneAsync() to insert a
                // new comment into the comments collection.
                await _commentsCollection
                      .WithWriteConcern(WriteConcern.WMajority)
                      .InsertOneAsync(newComment, cancellationToken : cancellationToken);

                return await _moviesRepository.GetMovieAsync(movieId.ToString(), cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Updates an existing comment. Only the comment owner can update the comment.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="movieId"></param>
        /// <param name="commentId"></param>
        /// <param name="comment"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>An UpdateResult</returns>
        public async Task<UpdateResult> UpdateCommentAsync(User user, ObjectId movieId, ObjectId commentId, string comment, CancellationToken cancellationToken = default)
        {
            // Implement UpdateOneAsync() to update an
            // existing comment. Remember that only the original
            // comment owner can update the comment!

            return await _commentsCollection.UpdateOneAsync(
            Builders<Comment>.Filter.Where(u => u.Email == user.Email && u.MovieId == movieId && u.Id == commentId),
            Builders<Comment>.Update.Set(c => c.Text, comment).Set(c => c.Name, user.Name),
            new UpdateOptions { IsUpsert = false }, // IsUpsert = Gets or sets a value indicating whether to insert the document if it doesn't already exist.
            cancellationToken);
        }

        /// <summary>
        ///     Deletes a comment. Only the comment owner can delete a comment.
        /// </summary>
        /// <param name="movieId"></param>
        /// <param name="commentId"></param>
        /// <param name="user"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The movie associated with the comment that is being deleted.</returns>
        public async Task<Movie> DeleteCommentAsync(ObjectId movieId, ObjectId commentId, User user, CancellationToken cancellationToken = default)
        {
            _commentsCollection.DeleteOne(
                Builders<Comment>.Filter.Where(c => c.MovieId == movieId && c.Id == commentId && c.Email == user.Email));

            return await _moviesRepository.GetMovieAsync(movieId.ToString(), cancellationToken);
        }

        public async Task<TopCommentsProjection> MostActiveCommentersAsync()
        {
            /**
                TODO Ticket: User Report
                Build a pipeline that returns the 20 most frequent commenters on the MFlix
                site. You can do this by counting the number of occurrences of a user's
                email in the `comments` collection.

                In addition, set the ReadConcern on the _commentsCollection to
                ensure the most accurate reads occur.
            */
            try
            {
                List<ReportProjection> result = null;
                // Return the 20 users who have commented the most on MFlix. You will need to use
                // the Group, Sort, Limit, and Project methods of the Aggregation pipeline.
                var projection = Builders<ReportProjection>.Projection.Include(m => m.Count);

                result = await _commentsCollection
                .Aggregate()
                .Group(x => x.Email, value => new ReportProjection() { Id = value.Key, Count = value.Count() })
                .SortByDescending(x => x.Count)
                .Limit(20)
                .Project<ReportProjection>(projection)
                .ToListAsync();

                return new TopCommentsProjection(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
